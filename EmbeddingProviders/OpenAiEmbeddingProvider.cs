using AiPOC.Configurations;
using System.Text;
using System.Text.Json;
using AiPOC.EmbeddingProviders;

namespace AiPOC.EmbeddingProviders
{
    public class OpenAiEmbeddingProvider(OpenAiConfiguration llmConfig, string collectionPrefixName) : EmbeddingProvider(llmConfig, EmbeddingProviderType.OpenAi, collectionPrefixName)
    {
        public override async Task<List<float>?> GetEmbeddingAsync(HttpClient client, string text)
        {
            if (string.IsNullOrWhiteSpace(this.ApiKey)) { Console.WriteLine($"Clé API {this.ProviderName} manquante."); return null; }
            var requestBody = new { model = this.ModelName, input = text };
            string jsonPayload = JsonSerializer.Serialize(requestBody);
            using var payload = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.ApiKey}");

            var response = await client.PostAsync(this.ApiUrl, payload);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) { Console.WriteLine($"{this.ProviderName} API Error: {response.StatusCode}"); return null; }

            using var doc = JsonDocument.Parse(responseStr);
            if (!doc.RootElement.TryGetProperty("data", out var dataElem) || dataElem.ValueKind != JsonValueKind.Array || dataElem.GetArrayLength() == 0 || !dataElem[0].TryGetProperty("embedding", out var embElem))
                return null;

            return embElem.EnumerateArray().Select(v => v.GetSingle()).ToList();
        }
    }
}
