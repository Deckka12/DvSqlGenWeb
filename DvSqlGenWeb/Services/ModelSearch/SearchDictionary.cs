using System.Text.Json;
using System.Text;

namespace DvSqlGenWeb.Services.ModelSearch
{

    public sealed class SearchDictionary
    {
        private readonly Dictionary<string, List<string>> _map;

        public SearchDictionary(Dictionary<string, List<string>> listValueMap) 
        {
            if (listValueMap == null) 
                throw new ArgumentNullException(nameof(listValueMap), "Справочник не передан (null)"); 

            _map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); 
            foreach (var kv in listValueMap) 
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) 
                    throw new ArgumentException("Обнаружен пустой ключ в справочнике (ожидался формат CardType.SectionAlias)"); 

                if (kv.Value == null || kv.Value.Count == 0) 
                    throw new ArgumentException($"Для ключа '{kv.Key}' не задан ни один поисковый маркер");

                _map[kv.Key] = kv.Value; 
            }
        }

        public IReadOnlyDictionary<string, List<string>> Map => _map; 

      
        public static SearchDictionary FromJson(string json) 
        {
            if (string.IsNullOrWhiteSpace(json)) 
                throw new ArgumentException("JSON справочника пуст"); 

            try 
            {
                using var doc = JsonDocument.Parse(json); 
                if (doc.RootElement.ValueKind != JsonValueKind.Object) 
                    throw new ArgumentException("Ожидался JSON-объект {...} для справочника"); 

                var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); 

                foreach (var prop in doc.RootElement.EnumerateObject()) 
                {
                    var key = prop.Name?.Trim(); 
                    if (string.IsNullOrWhiteSpace(key)) 
                        throw new ArgumentException("Обнаружен пустой ключ в JSON (ожидался формат CardType.SectionAlias)"); 

                    var value = prop.Value;
                    List<string> phrases;

                    switch (value.ValueKind) 
                    {
                        case JsonValueKind.String:
                            phrases = SplitPhrases(value.GetString()); 
                            break;

                        case JsonValueKind.Array: 
                            {
                                phrases = value 
                                    .EnumerateArray() 
                                    .Where(e => e.ValueKind == JsonValueKind.String)
                                    .Select(e => e.GetString()) 
                                    .Where(s => !string.IsNullOrWhiteSpace(s)) 
                                    .Select(s => s!.Trim()) 
                                    .ToList();

                                if (phrases.Count == 0) 
                                    throw new ArgumentException($"Для ключа '{key}' пустой массив фраз в JSON"); 
                                break; 
                            }

                        case JsonValueKind.Object: 
                            {
                                if (value.TryGetProperty("phrases", out var ph) && ph.ValueKind == JsonValueKind.Array) 
                                {
                                    phrases = ph
                                        .EnumerateArray() 
                                        .Where(e => e.ValueKind == JsonValueKind.String) 
                                        .Select(e => e.GetString()) 
                                        .Where(s => !string.IsNullOrWhiteSpace(s)) 
                                        .Select(s => s!.Trim())
                                        .ToList(); 

                                    if (phrases.Count == 0)
                                        throw new ArgumentException($"Для ключа '{key}' поле 'phrases' пусто");
                                }
                                else 
                                {
                                    throw new ArgumentException($"Для ключа '{key}' нельзя распознать структуру. Ожидались строка, массив строк или объект с полем 'phrases'"); // Сообщаем об ошибке
                                }
                                break;
                            }

                        default: 
                            throw new ArgumentException($"Для ключа '{key}' неподдерживаемый тип JSON: {value.ValueKind}"); 
                    }

                    phrases = phrases.Distinct(StringComparer.OrdinalIgnoreCase).ToList(); 
                    map[key] = phrases;
                }

                return new SearchDictionary(map); 
            }
            catch (JsonException ex) 
            {
                throw new ArgumentException("Некорректный JSON справочника: " + ex.Message, ex);
            }
        }


        public static SearchDictionary FromJsonFile(string path) 
        {
            if (string.IsNullOrWhiteSpace(path)) 
                throw new ArgumentException("Путь к JSON не задан"); 
            if (!File.Exists(path)) 
                throw new FileNotFoundException("Файл JSON не найден", path); 

            var json = File.ReadAllText(path, Encoding.UTF8);
            return FromJson(json); 
        }

        private static List<string> SplitPhrases(string? value) 
        {
            if (string.IsNullOrWhiteSpace(value)) 
                throw new ArgumentException("В справочнике обнаружено пустое значение фраз");

            return value
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) 
                .Select(s => s.Trim()) 
                .Where(s => s.Length > 0) 
                .ToList(); 
        }
    }
}
