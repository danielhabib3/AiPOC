using AiPOC.Configurations;
using Milvus.Client;
using AiPOC.EmbeddingProviders;
using AiPOC.Milvus;

namespace AiPOC.Repositories
{
    public class SuggestionRepository : MilvusRepository<string>
    {
        // </inheritdoc />
        public override async Task CreateCollectionAsync(MilvusClient client, EmbeddingProvider provider)
        {
            if (!await client.HasCollectionAsync(provider.CollectionName))
            {
                var fields = new List<FieldSchema>
                {
                    FieldSchema.Create<long>("id", isPrimaryKey: true, autoId: true),
                    FieldSchema.CreateVarchar("content", maxLength: 512),
                    FieldSchema.CreateFloatVector("embedding", provider.EmbeddingDimension)
                };

                await client.CreateCollectionAsync(provider.CollectionName, fields);
                Console.WriteLine($"Collection '{provider.CollectionName}' créée.");

                MilvusCollection createdCollection = client.GetCollection(provider.CollectionName);

                await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => createdCollection.CreateIndexAsync(
                    fieldName: "embedding",
                    indexType: IndexType.AutoIndex,
                    metricType: SimilarityMetricType.L2));

                Console.WriteLine("Index created.");
            }
        }

        // </inheritdoc />
        public override async Task EnsureDataInCollectionAsync(MilvusCollection collection, HttpClient httpClient, EmbeddingProvider provider, IReadOnlyCollection<string> data)
        {
            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.LoadAsync());
            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.WaitForCollectionLoadAsync(waitingInterval: TimeSpan.FromSeconds(2)));

            List<string> existingContents = await GetAllContentsAsync(collection);
            var existingSet = new HashSet<string>(existingContents, StringComparer.OrdinalIgnoreCase);

            var missing = data.Where(d => !existingSet.Contains(d)).ToList();
            if (missing.Count == 0)
            {
                Console.WriteLine("Aucun nouveau document à insérer.");
                return;
            }

            foreach (string text in missing)
            {
                List<float>? embedding = await provider.GetEmbeddingAsync(httpClient, text);
                if (embedding == null || embedding.Count == 0)
                {
                    Console.WriteLine($"Embedding manquant pour: {text}");
                    continue;
                }

                var textField = FieldData.CreateVarChar("content", new List<string> { text });
                var embeddingField = FieldData.CreateFloatVector("embedding", new List<ReadOnlyMemory<float>> { embedding.ToArray() });

                var rowData = new List<FieldData> { textField, embeddingField };

                await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.InsertAsync(rowData));
                Console.WriteLine($"Inserted: {text}");
            }

            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.WaitForFlushAsync(waitingInterval: TimeSpan.FromSeconds(2)));
            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.LoadAsync());
            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.WaitForCollectionLoadAsync(waitingInterval: TimeSpan.FromSeconds(2)));
        }

        // </inheritdoc />
        public override async Task<List<string>> GetAllContentsAsync(MilvusCollection collection)
        {
            var queryParameters = new QueryParameters
            {
                Limit = 16384
            };
            queryParameters.OutputFields.Add("content");

            var rows = await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.QueryAsync("id >= 0", queryParameters));
            return MilvusUtils.ExtractFieldValues(rows, "content");
        }

        // </inheritdoc />
        public override List<string> GetDataFromCsv(MilvusConfiguration config)
        {
            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                config.SuggestionDataPath
            );
            using var reader = new StreamReader(path);

            var lines = reader
                .ReadToEnd()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().Replace("\"", ""))
                .ToList();

            return lines;
        }
    }
}
