namespace DvSqlGenWeb.Services.ModelSearch
{

    public sealed class RouterTarget 
    {
        public string Key { get; } 
        public string CardType { get; } 
        public string Section { get; } 
        public IReadOnlyList<string> MatchedPhrases { get; } 

        public RouterTarget(string key, string cardType, string section, IEnumerable<string> matchedPhrases) 
        {
            Key = key ?? throw new ArgumentNullException(nameof(key), "RouterTarget: key не может быть null"); 
            CardType = cardType ?? throw new ArgumentNullException(nameof(cardType), "RouterTarget: cardType не может быть null"); 
            Section = section ?? throw new ArgumentNullException(nameof(section), "RouterTarget: section не может быть null");
            MatchedPhrases = matchedPhrases?.ToList() ?? throw new ArgumentNullException(nameof(matchedPhrases), "RouterTarget: matchedPhrases не может быть null"); 
        }

        public override string ToString() 
            => $"{CardType}.{Section} (фразы: {string.Join(", ", MatchedPhrases)})"; 
    }
}
