using AiPOC.Configurations;
using CsvHelper;
using CsvHelper.Configuration;
using Milvus.Client;
using System.Globalization;
using AiPOC.EmbeddingProviders;
using AiPOC.FAQ;
using AiPOC.Milvus;

namespace AiPOC.Repositories
{
    public class FaqRepository : MilvusRepository<Faq>
    {
        public readonly string FieldQuestion = "question";
        public readonly string FieldAnswer = "answer";
        public readonly string FieldQuestionVector = "question_vector";
        public readonly string FieldAnswerVector = "answer_vector";
        public readonly string FieldQaVector = "qa_vector";

        // </inheritdoc />
        public override async Task CreateCollectionAsync(MilvusClient client, EmbeddingProvider provider)
        {
            if (!await client.HasCollectionAsync(provider.CollectionName))
            {
                var fields = new List<FieldSchema>
                    {
                        FieldSchema.Create<long>("id", isPrimaryKey: true, autoId: true),
                        FieldSchema.CreateVarchar(FieldQuestion, maxLength: 1024),
                        FieldSchema.CreateVarchar(FieldAnswer, maxLength: 4096),
                        FieldSchema.CreateFloatVector(FieldQuestionVector, provider.EmbeddingDimension),
                        FieldSchema.CreateFloatVector(FieldAnswerVector, provider.EmbeddingDimension),
                        FieldSchema.CreateFloatVector(FieldQaVector, provider.EmbeddingDimension),
                    };

                await client.CreateCollectionAsync(provider.CollectionName, fields);
                Console.WriteLine($"Collection '{provider.CollectionName}' créée.");

                MilvusCollection created = client.GetCollection(provider.CollectionName);

                foreach (string vectorField in new[] { FieldQuestionVector, FieldAnswerVector, FieldQaVector })
                {
                    await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => created.CreateIndexAsync(
                        fieldName: vectorField,
                        indexType: IndexType.AutoIndex,
                        metricType: SimilarityMetricType.L2));
                }
                Console.WriteLine("Index créés sur question_vector, answer_vector, qa_vector.");
            }
        }

        // </inheritdoc />
        public override async Task EnsureDataInCollectionAsync(MilvusCollection collection, HttpClient httpClient, EmbeddingProvider provider, IReadOnlyCollection<Faq> data)
        {
            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.LoadAsync());
            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.WaitForCollectionLoadAsync(waitingInterval: TimeSpan.FromSeconds(2)));

            HashSet<string> existing = new(await GetAllQuestionsAsync(collection), StringComparer.OrdinalIgnoreCase);

            var missing = data.Where(f => !existing.Contains(f.Question)).ToList();
            if (missing.Count == 0)
            {
                Console.WriteLine("Aucun nouveau FAQ à insérer.");
                return;
            }

            foreach (var missingFaq in missing)
            {
                string combined = $"{missingFaq.Question} {missingFaq.Answer}";

                List<float>? qVec = await provider.GetEmbeddingAsync(httpClient, missingFaq.Question);
                List<float>? aVec = await provider.GetEmbeddingAsync(httpClient, missingFaq.Answer);
                List<float>? qaVec = await provider.GetEmbeddingAsync(httpClient, combined);

                if (qVec == null || aVec == null || qaVec == null)
                {
                    Console.WriteLine($"Embedding manquant pour: {missingFaq.Question}");
                    continue;
                }

                var rowData = new List<FieldData>
                {
                    FieldData.CreateVarChar(FieldQuestion, new List<string> { missingFaq.Question }),
                    FieldData.CreateVarChar(FieldAnswer,   new List<string> { missingFaq.Answer }),
                    FieldData.CreateFloatVector(FieldQuestionVector, new List<ReadOnlyMemory<float>> { qVec.ToArray() }),
                    FieldData.CreateFloatVector(FieldAnswerVector,   new List<ReadOnlyMemory<float>> { aVec.ToArray() }),
                    FieldData.CreateFloatVector(FieldQaVector,       new List<ReadOnlyMemory<float>> { qaVec.ToArray() }),
                };

                await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.InsertAsync(rowData));
                Console.WriteLine($"Inséré: {missingFaq.Question}");
            }

            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.WaitForFlushAsync(waitingInterval: TimeSpan.FromSeconds(2)));
            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.LoadAsync());
            await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.WaitForCollectionLoadAsync(waitingInterval: TimeSpan.FromSeconds(2)));
        }

        // </inheritdoc />
        public override Task<List<Faq>> GetAllContentsAsync(MilvusCollection collection)
        {
            throw new NotImplementedException();
        }

        // </inheritdoc />
        public override List<Faq> GetDataFromCsv(MilvusConfiguration config)
        {
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
            };

            using var reader = new StreamReader(config.FaqDataPath);
            using var csv = new CsvReader(reader, csvConfig);
            var faqs = csv.GetRecords<Faq>().ToList();
            return faqs;
        }

        /// <summary>
        /// Get all questions from the Milvus collection
        /// </summary>
        /// <param name="collection">Milvus collection</param>
        /// <returns></returns>
        public async Task<List<string>> GetAllQuestionsAsync(MilvusCollection collection)
        {
            var parameters = new QueryParameters { Limit = 16384 };
            parameters.OutputFields.Add(FieldQuestion);
            var rows = await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.QueryAsync("id >= 0", parameters));
            return MilvusUtils.ExtractFieldValues(rows, FieldQuestion);
        }
    }
}
