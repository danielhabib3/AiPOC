using AiPOC.Configurations;
using CsvHelper;
using CsvHelper.Configuration;
using Milvus.Client;
using System.Globalization;
using AiPOC.EmbeddingProviders;
using AiPOC.Milvus;
using AiPOC.Repositories;

namespace AiPOC.FAQ.WithMilvus
{
    class FaqWithMilvus
    {
        public static async Task RunFaqWithMilvus(EmbeddingServiceConfiguration embeddingConfig, FaqRepository faqRepository)
        {
            Console.WriteLine("Running FAQ with Milvus...");

            while (true)
            {
                EmbeddingProvider provider = ReadEmbeddingProvider(embeddingConfig);
                var client = new MilvusClient(embeddingConfig.Milvus.Host, provider.MilvusPort);

                await faqRepository.CreateCollectionAsync(client, provider);

                MilvusCollection collection = client.GetCollection(provider.CollectionName);
                using var httpClient = new HttpClient();

                var faqs = faqRepository.GetDataFromCsv(embeddingConfig.Milvus);

                await faqRepository.EnsureDataInCollectionAsync(collection, httpClient, provider, faqs);
                await PrintAllFaqsAsync(collection, faqRepository);

                while (true)
                {
                    Console.WriteLine("\n=== MENU FAQ ===");
                    Console.WriteLine("1. Rechercher par question");
                    Console.WriteLine("2. Rechercher par réponse");
                    Console.WriteLine("3. Rechercher (Q+R combinées)");
                    Console.WriteLine("4. Retour (changer de fournisseur)");
                    Console.WriteLine("0. Quitter");
                    Console.Write("Votre choix: ");
                    string? choice = Console.ReadLine();

                    if (choice == "0") return;
                    if (choice == "4") break;
                    if (choice is not ("1" or "2" or "3"))
                    {
                        Console.WriteLine("Choix invalide.");
                        continue;
                    }

                    string vectorField = choice switch { "1" => faqRepository.FieldQuestionVector, "2" => faqRepository.FieldAnswerVector, _ => faqRepository.FieldQaVector };

                    Console.Write("\nSaisissez votre texte de recherche: ");
                    string? queryText = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(queryText)) { Console.WriteLine("Texte vide. Annulé."); continue; }

                    List<float>? queryEmbedding = await provider.GetEmbeddingAsync(httpClient, queryText);
                    if (queryEmbedding == null || queryEmbedding.Count == 0)
                    {
                        Console.WriteLine("Impossible de générer l'embedding.");
                        continue;
                    }

                    var queryVectors = new List<ReadOnlyMemory<float>> { queryEmbedding.ToArray() };
                    var searchParameters = new SearchParameters();
                    searchParameters.OutputFields.Add(faqRepository.FieldQuestion);
                    searchParameters.OutputFields.Add(faqRepository.FieldAnswer);

                    int entityCount = await collection.GetEntityCountAsync();
                    int searchLimit = Math.Max(1, entityCount);

                    var result = await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.SearchAsync(
                        vectorFieldName: vectorField,
                        vectors: queryVectors,
                        metricType: SimilarityMetricType.L2,
                        limit: searchLimit,
                        parameters: searchParameters));

                    if (result.Scores.Count == 0)
                    {
                        Console.WriteLine("Aucun résultat.");
                        continue;
                    }

                    Console.WriteLine("\nRésultats (du plus similaire au moins similaire):");
                    List<string> questions = MilvusUtils.ExtractFieldValues(result.FieldsData, faqRepository.FieldQuestion);
                    List<string> answers = MilvusUtils.ExtractFieldValues(result.FieldsData, faqRepository.FieldAnswer);

                    var ranked = result.Scores.Select((score, i) => new
                    {
                        Score = score,
                        Question = i < questions.Count ? questions[i] : "(inconnu)",
                        Answer = i < answers.Count ? answers[i] : "(inconnu)"
                    }).OrderBy(r => r.Score).ToList();

                    for (int i = 0; i < ranked.Count; i++)
                    {
                        Console.WriteLine($"\n{i + 1}. Q : {ranked[i].Question}");
                        Console.WriteLine($"   R : {ranked[i].Answer}");
                        Console.WriteLine($"   Distance L2 : {ranked[i].Score:F6}");
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
                    case "1": return new GeminiEmbeddingProvider(config.Gemini, config.Milvus.FaqCollectionPrefix);
                    case "2": return new ClaudeEmbeddingProvider(config.Claude, config.Milvus.FaqCollectionPrefix);
                    case "3": return new MistralEmbeddingProvider(config.Mistral, config.Milvus.FaqCollectionPrefix);
                    case "4": return new OpenAiEmbeddingProvider(config.OpenAi, config.Milvus.FaqCollectionPrefix);
                    default: Console.WriteLine("Choix invalide."); break;
                }
            }
        }

        static async Task PrintAllFaqsAsync(MilvusCollection collection, FaqRepository faqRepository)
        {
            var parameters = new QueryParameters { Limit = 16384 };
            parameters.OutputFields.Add(faqRepository.FieldQuestion);
            parameters.OutputFields.Add(faqRepository.FieldAnswer);
            var rows = await MilvusUtils.ExecuteWithRateLimitRetryAsync(() => collection.QueryAsync("id >= 0", parameters));

            List<string> questions = MilvusUtils.ExtractFieldValues(rows, faqRepository.FieldQuestion);
            List<string> answers = MilvusUtils.ExtractFieldValues(rows, faqRepository.FieldAnswer);

            Console.WriteLine("\nFAQs présents dans la base :");
            if (questions.Count == 0)
            {
                Console.WriteLine("(aucun contenu)");
                return;
            }

            for (int i = 0; i < questions.Count; i++)
            {
                string ans = i < answers.Count ? answers[i] : "(inconnu)";
                Console.WriteLine($"{i + 1}. Q : {questions[i]}");
                Console.WriteLine($"   R : {ans}");
            }
        }
    }
}