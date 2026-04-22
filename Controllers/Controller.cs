using AiPOC.Configurations;
using Microsoft.AspNetCore.Mvc;
using Milvus.Client;
using AiPOC.EmbeddingProviders;
using AiPOC.Milvus;
using AiPOC.Repositories;

namespace AiPOC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController(EmbeddingServiceConfiguration embeddingConfig) : ControllerBase
    {

        private readonly EmbeddingServiceConfiguration _embeddingConfig = embeddingConfig;

        [HttpGet("faq/{question}")]
        public async Task<string> GetFaq(string question)
        {
            (MilvusCollection collection, EmbeddingProvider provider, HttpClient httpClient) = await InitializeGeminiMilvusAsync(_embeddingConfig, _embeddingConfig.Milvus.FaqCollectionPrefix, new FaqRepository());
            return await GetBestAnswerForQuestionAsync(collection, provider, httpClient, question, new FaqRepository()) ?? "No answer found";
        }

        [HttpGet("suggestion/{instruction}")]
        public async Task<string> GetSuggestion(string instruction)
        {
            (MilvusCollection collection, EmbeddingProvider provider, HttpClient httpClient) = await InitializeGeminiMilvusAsync(_embeddingConfig, _embeddingConfig.Milvus.SuggestionCollectionPrefix, new SuggestionRepository());
            return await GetBestSuggestionAsync(collection, provider, httpClient, instruction) ?? "No answer found";
        }

        /// <summary>
        /// This method performs a similarity search in the Milvus collection using the embedding of the provided query text.
        /// And it retrieves the best matching answer from the FAQ collection based on the similarity of the question embeddings.
        /// </summary>
        public static async Task<string?> GetBestAnswerForQuestionAsync(
            MilvusCollection collection,
            EmbeddingProvider provider,
            HttpClient httpClient,
            string queryText,
            FaqRepository faqRepository)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return null;
            }

            List<float>? queryEmbedding = await provider.GetEmbeddingAsync(httpClient, queryText);
            if (queryEmbedding == null || queryEmbedding.Count == 0)
            {
                return null;
            }

            var queryVectors = new List<ReadOnlyMemory<float>> { queryEmbedding.ToArray() };

            var searchParameters = new SearchParameters();
            searchParameters.OutputFields.Add(faqRepository.FieldAnswer);

            var result = await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.SearchAsync(
                vectorFieldName: faqRepository.FieldQuestionVector,
                vectors: queryVectors,
                metricType: SimilarityMetricType.L2,
                limit: 1,
                parameters: searchParameters));

            if (result.Scores.Count == 0)
            {
                return null;
            }

            List<string> answers = MilvusUtils.ExtractFieldValues(result.FieldsData, faqRepository.FieldAnswer);
            return answers.Count > 0 ? answers[0] : null;
        }

        /// <summary>
        /// This method performs a similarity search in the Milvus collection using the embedding of the provided query text.
        /// </summary>
        public static async Task<string?> GetBestSuggestionAsync(
            MilvusCollection collection,
            EmbeddingProvider provider,
            HttpClient httpClient,
            string queryText)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return null;
            }

            List<float>? queryEmbedding = await provider.GetEmbeddingAsync(httpClient, queryText);
            if (queryEmbedding == null || queryEmbedding.Count == 0)
            {
                return null;
            }

            var queryVectors = new List<ReadOnlyMemory<float>> { queryEmbedding.ToArray() };

            var searchParameters = new SearchParameters();
            searchParameters.OutputFields.Add("content");

            var result = await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.SearchAsync(
                vectorFieldName: "embedding",
                vectors: queryVectors,
                metricType: SimilarityMetricType.L2,
                limit: 1,
                parameters: searchParameters));

            if (result.Scores.Count == 0)
            {
                return null;
            }

            List<string> contents = MilvusUtils.ExtractFieldValues(result.FieldsData, "content");
            return contents.Count > 0 ? contents[0] : null;
        }

        /// <summary>
        /// This method initializes the Milvus client, creates the collection if it doesn't exist, 
        /// and ensures that the expected documents from the CSV are present in the vector database. 
        /// It returns a tuple containing the initialized Milvus client, the collection, the embedding provider, and an HTTP client for further interactions.
        /// </summary>
        public static async Task<(MilvusCollection Collection, EmbeddingProvider Provider, HttpClient HttpClient)> InitializeGeminiMilvusAsync<T>(EmbeddingServiceConfiguration config, string collectionPrefix, MilvusRepository<T> milvusRepository)
        {
            Console.WriteLine("\n[Initialisation automatique via Gemini...]");

            EmbeddingProvider provider = new GeminiEmbeddingProvider(config.Gemini, collectionPrefix);

            var client = new MilvusClient(config.Milvus.Host, provider.MilvusPort);

            await milvusRepository.CreateCollectionAsync(client, provider);
            MilvusCollection collection = client.GetCollection(provider.CollectionName);

            var data = milvusRepository.GetDataFromCsv(config.Milvus);

            var httpClient = new HttpClient();
            await milvusRepository.EnsureDataInCollectionAsync(collection, httpClient, provider, data);

            Console.WriteLine("[Initialisation terminée avec Gemini]");

            return (collection, provider, httpClient);
        }
    }
}
