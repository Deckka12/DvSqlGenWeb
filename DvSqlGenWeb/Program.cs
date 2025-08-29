using System.Buffers.Text;
using System.Threading;
using DvSqlGenWeb.Models;     // PromptDto
using DvSqlGenWeb.Services;   // Gpt4AllClient, ChromaDirect, SqlRagService

namespace DvSqlGenWeb
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ----- конфиг -----
            var cfg = builder.Configuration;

            string llmUrl = cfg["Llm:BaseUrl"] ?? "http://localhost:4891";
            string llmModel = cfg["Llm:Model"] ?? "Llama 3.1 8B Instruct 128k";
            string llmApiKey = cfg["Llm:ApiKey"] ?? "";
            double temperature = double.TryParse(cfg["Llm:Temperature"], out var t) ? t : 0.1;

            string chromaUrl = cfg["Chroma:BaseUrl"] ?? "http://localhost:8000";
            string collection = cfg["Rag:CollectionName"] ?? "dv_schema_web";
            int topK = int.TryParse(cfg["Rag:TopK"], out var k) ? k : 8;
            string schemaPath = cfg["Schema:Path"] ?? "App_Data/dv_schema.json";

            string openAiBaseUrl = cfg["OpenAI:BaseUrl"] ?? "https://api.openai.com";
            string openAiModel = cfg["OpenAI:Model"] ?? "gpt-4o-mini";
            string openAiKey = cfg["OpenAI:ApiKey"] ?? "";
            double openAiTemp = double.TryParse(cfg["OpenAI:Temperature"], out var ot) ? ot : 0.1;
            string embeddingModel = /*cfg["OpenAI:EmbeddingModel"] ??*/ "nomic-embed-text:latest";

            // ----- сервисы -----
            builder.Services.AddRazorPages();

            builder.Services.AddSingleton(new OpenAIClient(
                apiKey: openAiKey,
                baseUrl: openAiBaseUrl,
                model: openAiModel,
                temperature: openAiTemp,
                httpClient: new HttpClient
                {
                    BaseAddress = new Uri(openAiBaseUrl),
                    Timeout = TimeSpan.FromMinutes(2)
                }
            ));
            builder.Services.AddSingleton(new OllamaClient(
                baseUrl: "http://localhost:11434",
                model: llmModel,
                temperature: 0.1
            ));

            builder.Services.AddSingleton(new ChromaDirect(
                baseUrl: chromaUrl,
                openAiBaseUrl: openAiBaseUrl,
                openAiApiKey: openAiKey,
                embeddingModel: embeddingModel,
                httpClient: new HttpClient
                {
                    BaseAddress = new Uri(chromaUrl)
                }
            ));

            builder.Services.AddSingleton(new SqlRagService(
                schemaPath: Path.Combine(builder.Environment.ContentRootPath, schemaPath),
                collectionName: collection,
                topK: topK
            ));

            var app = builder.Build();

            // ----- pipeline как у тебя -----
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages().WithStaticAssets();

            // health
            app.MapGet("/health", () => Results.Ok(new { ok = true }));

            app.MapPost("/api/sql", async (PromptDto dto, ChromaDirect chroma, OllamaClient llm, SqlRagService rag, CancellationToken ct
             ) =>
                {
                    if (dto is null || string.IsNullOrWhiteSpace(dto.Question))
                        return Results.BadRequest(new { error = "Question is required" });

                    var sql = await rag.GenerateSqlAsync(dto.Question.Trim(), llm, chroma, ct);
                    if (string.IsNullOrWhiteSpace(sql))
                        return Results.UnprocessableEntity(new { error = "LLM did not produce valid SQL" });

                    return Results.Ok(new { sql });
                });


            app.Run();
        }
    }
}

namespace DvSqlGenWeb.Models
{
    public record PromptDto(string Question, bool Update);
}
