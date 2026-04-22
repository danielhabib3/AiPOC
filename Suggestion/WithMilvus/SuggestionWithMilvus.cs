using AiPOC.Configurations;
using Milvus.Client;
using AiPOC.EmbeddingProviders;
using AiPOC.Milvus;
using AiPOC.Repositories;

namespace AiPOC.Suggestion.WithMilvus
{
    class SuggestionWithMilvus
    {
        public static async Task RunSuggestionWithMilvus(EmbeddingServiceConfiguration embeddingConfig, SuggestionRepository suggestionRepository)
        {
            Console.WriteLine("Running Suggestion with Milvus...");

            while (true)
            {
                EmbeddingProvider provider = ReadEmbeddingProvider(embeddingConfig);
                var client = new MilvusClient(embeddingConfig.Milvus.Host, provider.MilvusPort);

                await suggestionRepository.CreateCollectionAsync(client, provider);

                MilvusCollection collection = client.GetCollection(provider.CollectionName);
                using var httpClient = new HttpClient();

                var data = suggestionRepository.GetDataFromCsv(embeddingConfig.Milvus);

                await suggestionRepository.EnsureDataInCollectionAsync(collection, httpClient, provider, data);
                await PrintAllContentsAsync(collection, suggestionRepository);

                while (true)
                {
                    Console.WriteLine("\n=== MENU ===");
                    Console.WriteLine("1. Rechercher un texte");
                    Console.WriteLine("2. Retour");
                    Console.WriteLine("0. Quitter");
                    Console.Write("Votre choix: ");
                    string? choice = Console.ReadLine();

                    if (choice == "0")
                    {
                        return;
                    }

                    if (choice == "2")
                    {
                        break;
                    }

                    if (choice != "1")
                    {
                        Console.WriteLine("Choix invalide.");
                        continue;
                    }

                    Console.Write("\nSaisissez un texte: ");
                    string? queryText = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(queryText))
                    {
                        Console.WriteLine("Texte vide. Annulé.");
                        continue;
                    }

                    var queryEmbedding = await provider.GetEmbeddingAsync(httpClient, queryText);
                    if (queryEmbedding == null || queryEmbedding.Count == 0)
                    {
                        Console.WriteLine("Embedding introuvable pour le texte saisi.");
                        continue;
                    }

                    var queryVectors = new List<ReadOnlyMemory<float>>
                    {
                        queryEmbedding.ToArray()
                    };

                    var searchParameters = new SearchParameters();
                    searchParameters.OutputFields.Add("content");

                    int entityCount = await collection.GetEntityCountAsync();
                    int searchLimit = Math.Max(1, entityCount);

                    var result = await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.SearchAsync(
                        vectorFieldName: "embedding",
                        vectors: queryVectors,
                        metricType: SimilarityMetricType.L2,
                        limit: searchLimit,
                        parameters: searchParameters));

                    if (result.Scores.Count > 0)
                    {
                        Console.WriteLine("\nRésultats (du plus similaire au moins similaire):");

                        List<string> contents = MilvusUtils.ExtractFieldValues(result.FieldsData, "content");
                        var rankedResults = result.Scores
                            .Select((score, index) => new
                            {
                                Score = score,
                                Content = index < contents.Count ? contents[index] : "(no content found)"
                            })
                            .OrderBy(r => r.Score)
                            .ToList();

                        for (int i = 0; i < rankedResults.Count; i++)
                        {
                            Console.WriteLine($"{i + 1}. Text: {rankedResults[i].Content}");
                            Console.WriteLine($"   Similarity (L2 distance): {rankedResults[i].Score}");
                        }
                    }
                }
            }
        }

        static EmbeddingProvider ReadEmbeddingProvider(EmbeddingServiceConfiguration config)
        {
            while (true)
            {
                Console.WriteLine("\n=== CHOIX IA ===");
                Console.WriteLine("1. Gemini");
                Console.WriteLine("2. Claude (VoyageAI)");
                Console.WriteLine("3. Mistral");
                Console.WriteLine("4. OpenAI");
                Console.Write("Votre choix: ");
                string? choice = Console.ReadLine();

                switch (choice)
                {
                    case "1": return new GeminiEmbeddingProvider(config.Gemini, config.Milvus.SuggestionCollectionPrefix);
                    case "2": return new ClaudeEmbeddingProvider(config.Claude, config.Milvus.SuggestionCollectionPrefix);
                    case "3": return new MistralEmbeddingProvider(config.Mistral, config.Milvus.SuggestionCollectionPrefix);
                    case "4": return new OpenAiEmbeddingProvider(config.OpenAi, config.Milvus.SuggestionCollectionPrefix);
                    default: Console.WriteLine("Choix invalide."); break;
                }
            }
        }

        static async Task PrintAllContentsAsync(MilvusCollection collection, SuggestionRepository suggestionRepository)
        {
            List<string> contents = await suggestionRepository.GetAllContentsAsync(collection);

            Console.WriteLine("\nTextes présents dans la base :");
            if (contents.Count == 0)
            {
                Console.WriteLine("(aucun contenu)");
                return;
            }

            for (int i = 0; i < contents.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {contents[i]}");
            }
        }
    }
}