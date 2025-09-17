using DvSqlGenWeb.Context.Models;
using Microsoft.EntityFrameworkCore;

namespace DvSqlGenWeb.Context
{
    public class EfEmbeddingStore
    {
        private readonly RagDbContext _db;

        public EfEmbeddingStore(RagDbContext db)
        {
            _db = db;
        }

        public async Task UpsertAsync(string id, string text, float[] embedding, Dictionary<string, object> meta)
        {
            var chunk = await _db.Chunks.FindAsync(id);
            if (chunk == null)
            {
                chunk = new Chunk { Id = id };
                _db.Chunks.Add(chunk);
            }

            chunk.Text = text;
            chunk.CardType = meta.ContainsKey("CardType") ? meta["CardType"]?.ToString() : null;
            chunk.SectionAlias = meta.ContainsKey("SectionAlias") ? meta["SectionAlias"]?.ToString() : null;
            chunk.SectionId = meta.ContainsKey("SectionId") ? meta["SectionId"]?.ToString() : null;
            chunk.Field = meta.ContainsKey("Field") ? meta["Field"]?.ToString() : null;

            if (meta.ContainsKey("Synonyms"))
            {
                if (meta["Synonyms"] is IEnumerable<string> s)
                    chunk.Synonyms = string.Join(",", s);
                else
                    chunk.Synonyms = meta["Synonyms"]?.ToString();
            }

            chunk.Embedding = FloatArrayToBytes(embedding);
            chunk.Dim = embedding.Length;

            await _db.SaveChangesAsync();
        }


        public async Task<List<(Chunk Chunk, double Score)>> QueryAsync(float[] queryEmbedding, int topK = 5)
        {
            var all = await _db.Chunks.ToListAsync();
            var results = all
                .Select(c => (c, Score: Cosine(queryEmbedding, BytesToFloatArray(c.Embedding, c.Dim))))
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .ToList();

            return results;
        }

        private static double Cosine(float[] a, float[] b)
        {
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-12);
        }

        private static byte[] FloatArrayToBytes(float[] arr)
        {
            var bytes = new byte[arr.Length * sizeof(float)];
            Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static float[] BytesToFloatArray(byte[] bytes, int dim)
        {
            var arr = new float[dim];
            Buffer.BlockCopy(bytes, 0, arr, 0, dim * sizeof(float));
            return arr;
        }
    }

}
