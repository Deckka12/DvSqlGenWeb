using System.Text.RegularExpressions;

namespace DvSqlGenWeb.Services.ModelSearch
{

    public static class SearchRouter 
    {
        private static readonly Regex SpaceRegex = new Regex(@"\s+", RegexOptions.Compiled); 

        public static List<RouterTarget> DetectTargets(string userQuery, SearchDictionary dict) 
        {
            if (dict == null)
                throw new ArgumentNullException(nameof(dict), "Справочник для маршрутизации не задан"); 

            if (string.IsNullOrWhiteSpace(userQuery)) 
                return new List<RouterTarget>(); 

            var normalizedQuery = Normalize(userQuery);
            var hits = new List<RouterTarget>();

            foreach (var kv in dict.Map) 
            {
                var key = kv.Key; 
                var phrases = kv.Value; 

                var parts = key.Split('.'); 
                if (parts.Length != 2) 
                    throw new ArgumentException($"Ключ '{key}' должен быть в формате 'CardType.SectionAlias'"); 

                var cardType = parts[0].Trim(); 
                var section = parts[1].Trim(); 

                var matched = new List<string>(); 
                foreach (var phrase in phrases)
                {
                    var np = Normalize(phrase);
                    if (np.Length == 0) continue; 

                    if (normalizedQuery.Contains(np, StringComparison.OrdinalIgnoreCase)) 
                    {
                        matched.Add(phrase); 
                    }
                }

                if (matched.Count > 0)
                {
                    hits.Add(new RouterTarget(key, cardType, section, matched));
                }
            }

            return hits.OrderByDescending(h => h.MatchedPhrases.Count).ToList();
        }


        public static (List<RouterTarget> Targets, string CleanedText) DetectTargetsAndClean(string userQuery, SearchDictionary dict)
        {
            var targets = DetectTargets(userQuery, dict);
            var cleaned = userQuery;

            foreach (var t in targets)
            {
                foreach (var phrase in t.MatchedPhrases)
                {
                    cleaned = Regex.Replace(cleaned, Regex.Escape(phrase), "", RegexOptions.IgnoreCase);
                }
            }

            return (targets, cleaned.Trim());
        }

        public static Dictionary<string, object>? BuildChromaWhereV1(IReadOnlyCollection<RouterTarget> targets, string cardTypeField = "cardType", string sectionField = "section")
        {
            if (targets == null) 
                throw new ArgumentNullException(nameof(targets));

            if (targets.Count == 0) 
                return null;

            var byType = targets
                .GroupBy(t => t.CardType, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    CardType = g.Key,
                    Sections = g.Select(t => t.Section)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList()
                })
                .ToList();

            if (byType.Count == 1)
            {
                var g = byType[0];
                var andList = new List<object>
                {
                    new Dictionary<string, object> { [cardTypeField] = g.CardType }
                };

                if (g.Sections.Count == 1)
                    andList.Add(new Dictionary<string, object> { [sectionField] = g.Sections[0] });
                else
                    andList.Add(new Dictionary<string, object>
                    {
                        [sectionField] = new Dictionary<string, object> { ["$in"] = g.Sections }
                    });

                return new Dictionary<string, object> { ["$and"] = andList };
            }

            var orList = new List<object>();
            foreach (var g in byType)
            {
                var andList = new List<object>
                {
                    new Dictionary<string, object> { [cardTypeField] = g.CardType }
                };

                if (g.Sections.Count == 1)
                    andList.Add(new Dictionary<string, object> { [sectionField] = g.Sections[0] });
                else
                    andList.Add(new Dictionary<string, object>
                    {
                        [sectionField] = new Dictionary<string, object> { ["$in"] = g.Sections }
                    });

                orList.Add(new Dictionary<string, object> { ["$and"] = andList });
            }

            return new Dictionary<string, object> { ["$or"] = orList };
        }
        public static List<string> SplitToPhrases(string userQuery)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
                return new List<string>();

            return userQuery
                .Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }
        public static Dictionary<string, object>? BuildChromaWhereV2(
            IReadOnlyCollection<RouterTarget> targets,
            string cardTypeField = "cardType",
            string sectionField = "section",
            string phraseField = "text")
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));
            if (targets.Count == 0)
                return null;

            var orList = new List<object>();

            foreach (var g in targets.GroupBy(t => t.CardType, StringComparer.OrdinalIgnoreCase))
            {
                var andList = new List<object>
                    {
                        new Dictionary<string, object> { [cardTypeField] = g.Key }
                    };

                var sections = g.Select(t => t.Section)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

                if (sections.Count == 1)
                    andList.Add(new Dictionary<string, object> { [sectionField] = sections[0] });
                else if (sections.Count > 1)
                    andList.Add(new Dictionary<string, object>
                    {
                        [sectionField] = new Dictionary<string, object> { ["$in"] = sections }
                    });

                var phrases = g.SelectMany(t => t.MatchedPhrases).Distinct().ToList();
                if (phrases.Count == 1)
                {
                    andList.Add(new Dictionary<string, object> { [phraseField] = phrases[0] });
                }
                else if (phrases.Count > 1)
                {
                    andList.Add(new Dictionary<string, object>
                    {
                        [phraseField] = new Dictionary<string, object> { ["$in"] = phrases }
                    });
                }

                orList.Add(new Dictionary<string, object> { ["$and"] = andList });
            }

            return new Dictionary<string, object> { ["$or"] = orList };
        }



        private static string Normalize(string s) 
        {
            s = s ?? string.Empty;
            s = s.ToLowerInvariant();
            s = SpaceRegex.Replace(s, " ");
            return s.Trim();
        }
    }
}
