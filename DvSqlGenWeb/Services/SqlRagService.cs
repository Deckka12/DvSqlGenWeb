using System.Text;
using System.Text.Json;
using DvSqlGenWeb.Models;
using DvSqlGenWeb.Services.ModelSearch;

namespace DvSqlGenWeb.Services
{
    public class SqlRagService
    {
        private readonly string _schemaPath;
        private readonly string _collectionName;
        private readonly int _topK;
        private string? _collectionId;
        private volatile bool _indexed;
        private readonly object _lock = new();
        private readonly string _routerJsonPath; // путь к роутеру


        public SqlRagService(string schemaPath, string collectionName, int topK = 8, string routerJsonPath = "App_Data/router.json")
        {
            _schemaPath = schemaPath;
            _collectionName = collectionName;
            _topK = Math.Max(3, topK);
            _routerJsonPath = routerJsonPath;
        }

        //        public async Task<string> GenerateSqlAsync(string question, Gpt4AllClient llm, ChromaDirect chroma, CancellationToken ct = default)
        //        {
        //            await EnsureIndexedAsync(chroma, ct);

        //            var relevant = await chroma.QueryAsync(_collectionId!, question, n: _topK);
        //            var context = string.Join("\n---\n", relevant.Take(Math.Min(_topK, relevant.Count)));

        //            // «мягкий» системный промпт как в WinForms-клиенте
        //            var systemPrompt = """
        //Ты — генератор SQL для DocsVision.

        //Правила вывода:
        //- Не выводи <think> или рассуждения. Только финальный ответ.
        //- Ответ строго в одном блоке ```sql ... ``` без пояснений.
        //""";

        //            // Никакой доп. пост-валидации — пусть Sanitize клиента решает, как в WinForms
        //            return await llm.ChatAsync(systemPrompt, userPrompt: question, context: context, ct: ct);
        //        }

        // 1) просто добавь перегрузку
        public async Task<string> GenerateSqlAsync(string question, OllamaClient llm, ChromaDirect chroma, CancellationToken ct = default)
        {
            // if(update)
            await EnsureIndexedAsync(chroma, ct);

            _collectionId = await chroma.EnsureCollectionAsync(_collectionName);

            var dict = SearchDictionary.FromJsonFile(_routerJsonPath);

            var targets = SearchRouter.DetectTargets(question, dict);
            Dictionary<string, object>? where = null;
            if (targets.Count > 0)
                where = SearchRouter.BuildChromaWhereV1(targets, "cardType", "section");

            var relevant = await chroma.QueryAsync(_collectionId!, question, n: _topK, where: where);

            var context = string.Join("\n---\n", relevant.Take(Math.Min(_topK, relevant.Count)));

            var systemPrompt = """
            Ты — генератор SQL для DocsVision.

            Правила вывода:
            - Только таблицы вида dvtable_{GUID}.
            - Никаких RefStaff.Employees и т.п. — только dvtable_{...}.
            - Колонки и JOIN-ключи — только из контекста.
            - Нулевой GUID сравнивай строкой '00000000-0000-0000-0000-000000000000'.
            - Ответ строго один SQL без пояснений.
            - Оберни ответ в ```sql ... ``` .
            - Поля типа REF хранят RowID целевой секции. Всегда связывай REF → Target.RowID.
            - Поля типа REFCard хранят InstanceID целевой секции. Всегда связывай REFCard → Target.InstanceID
            - НИКОГДА не связывай REF с Target.InstanceID.
            - Всегда ставь WITH (NOLOCK) на таблицы
            - Формат запроса должен быть 
            select 
                ** from ***
                join ** as **  WITH (NOLOCK)
                    on ** = **
            """;

            return await llm.ChatAsync(systemPrompt, userPrompt: question, context: context, ct: ct);
        }


        // в том же классе SqlRagService
        public async Task<string> GenerateSqlAsync(string question, bool update, OpenAIClient llm, ChromaDirect chroma, CancellationToken ct = default)
        {
           // if(update)
                await EnsureIndexedAsync(chroma, ct);

            _collectionId = await chroma.EnsureCollectionAsync(_collectionName);

            var dict = SearchDictionary.FromJsonFile(_routerJsonPath);

            //var targets = SearchRouter.DetectTargets(question, dict);
            //var (targets, cleanedText) = SearchRouter.DetectTargetsAndClean(question, dict);
            //Dictionary<string, object>? where = null;
            //if (targets.Count > 0)
            //    where = SearchRouter.BuildChromaWhereV1(targets, "cardType", "section");

            var phrases = SearchRouter.SplitToPhrases(question);
            var conditions = new List<Dictionary<string, object>>();
            phrases = phrases.Where(x => x.Length > 2).ToList();

            foreach (var p in phrases)
            {
                // Условие по самому полю
                conditions.Add(new Dictionary<string, object>
                {
                    ["Field"] = p.ToLower()
                });

                // TODO: научить делать поиск по массиву
                //conditions.Add(new Dictionary<string, object>
                //{
                //    ["Synonyms"] = new Dictionary<string, object>
                //    {
                //        ["$contains"] = p.ToLower()
                //    }
                //});
            }

            Dictionary<string, object>? where = null;
            if (conditions.Count > 0)
            {
                where = new Dictionary<string, object>
                {
                    ["$or"] = conditions
                };
            }

            Dictionary<string, object>? whereDoc = null;
            if (phrases.Count > 0)
            {
                whereDoc = new Dictionary<string, object>
                {
                    ["$or"] = phrases.Select(p =>
                        new Dictionary<string, object>
                        {
                            ["$contains"] = p.ToLower()
                        }).ToList<object>()
                };
            }

            var relevant = await chroma.QueryAsync(_collectionId!, question, n: _topK, where: where, where_doc: whereDoc);

            //TODO: надо подумать, может стоит убрать синонимы для контекста, дабы зачем они модели. Тестриуем!!
            var result = relevant.Select(r =>
            {
                var lines = r.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var table = lines.FirstOrDefault(l => l.StartsWith("TABLE:", StringComparison.Ordinal));
                var section = lines.FirstOrDefault(l => l.StartsWith("SECTION:", StringComparison.Ordinal));
                return (lines, table, section);
                }).Where(x => x.table is not null && x.section is not null)
                .GroupBy(x => (x.table!, x.section!))
                .Select(g =>
                {
                
                    var fields = g.SelectMany(x => x.lines.Where(l => l.StartsWith("- ", StringComparison.Ordinal)))
                                  .Distinct();
                    var synonyms = g.SelectMany(x => x.lines.Where(l => l.StartsWith("SYNONYMS:", StringComparison.Ordinal)))
                                    .Distinct(); 
                    return string.Join('\n',
                        new[] { g.Key.Item1, g.Key.Item2, "FIELDS:" }
                            .Concat(fields)
                            .Concat(synonyms));
            }).ToList();


            var context = string.Join("\n---\n", result.Take(_topK));

            var systemPrompt = """
            Ты — генератор SQL для DocsVision.

            Правила вывода:
            - Только таблицы вида dvtable_{GUID}.
            - Никаких RefStaff.Employees и т.п. — только dvtable_{...}.
            - Колонки и JOIN-ключи — только из контекста.
            - Нулевой GUID сравнивай строкой '00000000-0000-0000-0000-000000000000'.
            - Ответ строго один SQL без пояснений.
            - Оберни ответ в ```sql ... ``` .
            - Поля типа REF хранят RowID целевой секции. Всегда связывай REF → Target.RowID.
            - Поля типа REFCard хранят InstanceID целевой секции. Всегда связывай REFCard → Target.InstanceID
            - НИКОГДА не связывай REF с Target.InstanceID.
            - Всегда ставь WITH (NOLOCK) на таблицы
            - Формат запроса должен быть 
            select 
                ** from ***
                join ** as **  WITH (NOLOCK)
                    on ** = **
            - CardTaskList.Tasks не является конечным результатом, завершаем запрос до конца до CardTask.MainInfo
            """;
            return await llm.ChatAsync(systemPrompt, userPrompt: question, context: context, ct: ct);
        }


        private async Task EnsureIndexedAsync(ChromaDirect chroma, CancellationToken ct)
        {
            if (_indexed)
                return;
            lock (_lock)
                if (_indexed)
                    return;

            _collectionId = await chroma.EnsureCollectionAsync(_collectionName);

            if (!File.Exists(_schemaPath))
                throw new FileNotFoundException($"Схема не найдена: {_schemaPath}");

            var json = await File.ReadAllTextAsync(_schemaPath, ct);
            var schema = JsonSerializer.Deserialize<DVSchema>(json) ?? new DVSchema();

            var chunks = ChunkBuilder.BuildChunks(schema);

            await chroma.UpsertAsync(
                 _collectionId!,
                 chunks.Select((c, i) => (
                     Id: $"doc_{i:D6}",
                     Text: c.Content,
                     Metadata: new Dictionary<string, object>
                     {

                         ["CardType"] = c.CardType,
                         ["SectionAlias"] = c.SectionAlias,
                         ["SectionId"] = c.SectionId,
                         ["Field"] = c.FieldAlias.ToLower(),
                         ["Synonyms"] = schema.sections[c.SectionId].fields
                                             .FirstOrDefault(f => f.alias == c.FieldAlias)?.synonyms
                                             ?? new List<string>()
                     }
                 )).ToList()
             );


            lock (_lock) 
                _indexed = true;
        }
    }
}
