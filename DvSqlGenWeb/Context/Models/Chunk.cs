using System.ComponentModel.DataAnnotations;

namespace DvSqlGenWeb.Context.Models
{
    public class Chunk
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Text { get; set; } = null!;

        public string? CardType { get; set; }
        public string? SectionAlias { get; set; }
        public string? SectionId { get; set; }
        public string? Field { get; set; }
        public string? Synonyms { get; set; }

        public byte[] Embedding { get; set; } = Array.Empty<byte>();

        public int Dim { get; set; }
    }
}
