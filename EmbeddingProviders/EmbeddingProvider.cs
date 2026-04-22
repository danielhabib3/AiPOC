using AiPOC.Configurations;

namespace AiPOC.EmbeddingProviders
{
    public abstract class EmbeddingProvider
    {
        public string ProviderName { get; }
        protected string ApiKey { get; }
        protected string ApiUrl { get; }
        public string ModelName { get; }
        public int MilvusPort { get; }
        public int EmbeddingDimension { get; }
        public string CollectionName { get; }

        protected EmbeddingProvider(LlmConfiguration llmConfig, EmbeddingProviderType providerType, string collectionPrefixName)
        {
            ProviderName = providerType.ToString();
            ApiKey = llmConfig.ApiKey;
            ApiUrl = llmConfig.ApiUrl;
            ModelName = llmConfig.ModelName;
            MilvusPort = llmConfig.MilvusPort;
            EmbeddingDimension = llmConfig.Dimension;
            CollectionName = $"{collectionPrefixName}_{this.ProviderName.ToLower()}";
        }

        public abstract Task<List<float>?> GetEmbeddingAsync(HttpClient client, string text);
    }
}
