using AiPOC.Configurations;
using System.Text;
using System.Text.Json;

namespace AiPOC.EmbeddingProviders
{
    public class GeminiEmbeddingProvider(GeminiConfiguration llmConfig, string collectionPrefixName) : EmbeddingProvider(llmConfig, EmbeddingProviderType.Gemini, collectionPrefixName)
    {
        public override async Task<List<float>?> GetEmbeddingAsync(HttpClient client, string text)
        {
            if (string.IsNullOrWhiteSpace(this.ApiKey)) { Console.WriteLine($"Clé API {this.ProviderName} manquante."); return null; }

            var requestBody = new
            {
                model = this.ModelName,
                task_type = "SEMANTIC_SIMILARITY",
                content = new { parts = new[] { new { text } } }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            using var payload = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-goog-api-key", this.ApiKey);

            var response = await client.PostAsync(this.ApiUrl, payload);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) { Console.WriteLine($"{this.ProviderName} API Error: {response.StatusCode}"); return null; }

            using var doc = JsonDocument.Parse(responseStr);
            if (!doc.RootElement.TryGetProperty("embedding", out var embElem) || !embElem.TryGetProperty("values", out var values))
                return null;

            return values.EnumerateArray().Select(v => v.GetSingle()).ToList();
        }
    }
}
