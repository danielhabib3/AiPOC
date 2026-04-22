namespace AiPOC.Configurations
{
    public class EmbeddingServiceConfiguration
    {
        public MilvusConfiguration Milvus { get; set; } = null!;
        public GeminiConfiguration Gemini { get; set; } = null!;
        public ClaudeConfiguration Claude { get; set; } = null!;
        public MistralConfiguration Mistral { get; set; } = null!;
        public OpenAiConfiguration OpenAi { get; set; } = null!;
    }

    public class LlmConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int MilvusPort { get; set; }
        public int Dimension { get; set; }
    }

    public class MilvusConfiguration
    {
        public string Host { get; set; } = "127.0.0.1";
        public string FaqCollectionPrefix { get; set; } = string.Empty;
        public string SuggestionCollectionPrefix { get; set; } = string.Empty;
        public string FaqDataPath { get; set; } = string.Empty;
        public string SuggestionDataPath { get; set; } = string.Empty;
    }

    public class GeminiConfiguration : LlmConfiguration;

    public class ClaudeConfiguration : LlmConfiguration;

    public class MistralConfiguration : LlmConfiguration;

    public class OpenAiConfiguration : LlmConfiguration;
}
