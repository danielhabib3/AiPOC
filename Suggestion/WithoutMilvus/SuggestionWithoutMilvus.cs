using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using AiPOC.EmbeddingProviders;
using CsvHelper;
using CsvHelper.Configuration;
using AiPOC.Configurations;

namespace AiPOC.Suggestion.WithoutMilvus
{
    class SuggestionWithoutMilvus
    {
        public static async Task RunSuggestionWithoutMilvus(EmbeddingServiceConfiguration embeddingConfig)
        {
            Console.WriteLine("Running Suggestion Without Milvus...");

            using var httpClient = new HttpClient();
            string projectDirectory = GetSourceFolderPath();
            
            // Fichier source contenant les instructions/textes
            string sourceCsvPath = Path.Combine(projectDirectory, "instructions_chubb.csv");

            while (true)
            {
                EmbeddingProvider provider = ReadEmbeddingProvider(embeddingConfig);
                string vectorizedFileName = $"embeddings_{provider.ProviderName.ToLower()}.csv";

                // Assure que tout est vectorisé automatiquement avant d'afficher le menu
                await EnsureDataVectorizedAsync(httpClient, provider, projectDirectory, vectorizedFileName, sourceCsvPath);

                while (true)
                {
                    Console.WriteLine($"\n=== MENU Suggestion Without Milvus ({provider.ProviderName}) ===");
                    Console.WriteLine("1. Lire le CSV et trouver le plus proche (cosine similarity)");
                    Console.WriteLine("2. Retour (changer de LLM)");
                    Console.WriteLine("0. Quitter");
                    Console.Write("Votre choix: ");

                    string? choice = Console.ReadLine();

                    if (choice == "0")
                    {
                        return;
                    }
                    else if (choice == "2")
                    {
                        break;
                    }
                    else if (choice == "1")
                    {
                        await FindClosestVectorByCosineSimilarity(httpClient, provider, projectDirectory, vectorizedFileName);
                    }
                    else
                    {
                        Console.WriteLine("Choix invalide.");
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
                    case "1": return new GeminiEmbeddingProvider(config.Gemini, "instructions");
                    case "2": return new ClaudeEmbeddingProvider(config.Claude, "instructions");
                    case "3": return new MistralEmbeddingProvider(config.Mistral, "instructions");
                    case "4": return new OpenAiEmbeddingProvider(config.OpenAi, "instructions");
                    default: Console.WriteLine("Choix invalide."); break;
                }
            }
        }

        static async Task EnsureDataVectorizedAsync(HttpClient client, EmbeddingProvider provider, string projectDirectory, string destFileName, string sourceCsvPath)
        {
            if (!File.Exists(sourceCsvPath))
            {
                Console.WriteLine($"[ATTENTION] Le fichier source {sourceCsvPath} n'existe pas.");
                return;
            }

            // Lire les textes depuis le fichier source
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";", // Ajustez au besoin selon votre CSV (" ; " ou " , ")
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            };

            var sourceTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StreamReader(sourceCsvPath))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    // Suppose que la colonne s'appelle "Texte"
                    string text = csv.GetField<string>("Texte") ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sourceTexts.Add(text.Trim());
                    }
                }
            }

            string destPath = Path.Combine(projectDirectory, destFileName);
            var existingTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Créer le fichier d'embeddings avec l'en-tête s'il n'existe pas
            if (!File.Exists(destPath))
            {
                using var initialWriter = new StreamWriter(destPath);
                await initialWriter.WriteLineAsync("Texte;Vecteur");
            }
            else
            {
                // Lire ce qui est déjà vectorisé pour éviter les doublons
                string[] lines = await File.ReadAllLinesAsync(destPath);
                foreach (string line in lines.Skip(1)) // Skip header
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    string[] parts = line.Split(';', 2);
                    if (parts.Length == 2)
                    {
                        string csvText = parts[0].Trim().Trim('"');
                        existingTexts.Add(csvText);
                    }
                }
            }

            // Obtenir ce qui manque
            var textsToVectorize = sourceTexts.Where(t => !existingTexts.Contains(t)).ToList();

            if (textsToVectorize.Count == 0)
            {
                Console.WriteLine($"[OK] ({provider.ProviderName}) Tous les textes sont déjà vectorisés.");
                return;
            }

            Console.WriteLine($"\n({provider.ProviderName}) Vectorisation de {textsToVectorize.Count} texte(s) manquant(s)...");

            try
            {
                // Append au fichier existant
                using var writer = new StreamWriter(destPath, append: true);
                
                foreach (var text in textsToVectorize)
                {
                    Console.Write($"Processing: \"{text[..Math.Min(20, text.Length)]}...\" ");

                    List<float>? embedding = await provider.GetEmbeddingAsync(client, text);
                    if (embedding is null || embedding.Count == 0)
                    {
                        Console.WriteLine("[ERROR: No embedding values found, LLM a échoué]");
                        continue;
                    }

                    string vectorString = string.Join(",", embedding.Select(v => v.ToString(CultureInfo.InvariantCulture)));
                    string sanitizedText = text.Replace("\"", "'"); // Évite de casser le CSV
                    
                    await writer.WriteLineAsync($"\"{sanitizedText}\";\"{vectorString}\"");
                    Console.WriteLine("[OK]");

                    await Task.Delay(200); // Respecter les rate limits potentiels
                }
                Console.WriteLine("Mise à jour terminée !");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nCritical error during vectorization: {ex.Message}");
            }
        }

        static async Task FindClosestVectorByCosineSimilarity(HttpClient client, EmbeddingProvider provider, string projectDirectory, string fileName)
        {
            Console.Write("Saisissez un texte: ");
            string? inputText = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(inputText))
            {
                Console.WriteLine("Texte vide. Annulé.");
                return;
            }

            string filePath = Path.Combine(projectDirectory, fileName);

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Fichier introuvable: {filePath}.");
                return;
            }

            List<float>? queryVector = await provider.GetEmbeddingAsync(client, inputText);
            if (queryVector is null || queryVector.Count == 0)
            {
                Console.WriteLine("Impossible de générer le vecteur pour le texte saisi.");
                return;
            }

            string[] lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length <= 1)
            {
                Console.WriteLine("Le fichier CSV ne contient pas de données.");
                return;
            }

            string? bestText = null;
            double bestScore = double.NegativeInfinity;

            foreach (string line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(';', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                string csvText = parts[0].Trim().Trim('"');
                string vectorPart = parts[1].Trim().Trim('"');
                List<float>? candidateVector = ParseVector(vectorPart);

                if (candidateVector is null || candidateVector.Count == 0)
                {
                    continue;
                }

                double score = CosineSimilarity(queryVector, candidateVector);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestText = csvText;
                }
            }

            if (bestText is null)
            {
                Console.WriteLine("Aucun vecteur valide trouvé dans le fichier CSV.");
                return;
            }

            Console.WriteLine("\nRésultat le plus proche:");
            Console.WriteLine($"Texte: {bestText}");
            Console.WriteLine($"Cosine similarity: {bestScore.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Fichier utilisé: {filePath}");
        }

        static List<float>? ParseVector(string vectorText)
        {
            string[] items = vectorText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var result = new List<float>(items.Length);

            foreach (string item in items)
            {
                if (!float.TryParse(item, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    return null;
                }

                result.Add(value);
            }

            return result;
        }

        static double CosineSimilarity(IReadOnlyList<float> vectorA, IReadOnlyList<float> vectorB)
        {
            int length = Math.Min(vectorA.Count, vectorB.Count);
            if (length == 0)
            {
                return double.NegativeInfinity;
            }

            double dot = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < length; i++)
            {
                double a = vectorA[i];
                double b = vectorB[i];
                dot += a * b;
                normA += a * a;
                normB += b * b;
            }

            if (normA == 0 || normB == 0)
            {
                return double.NegativeInfinity;
            }

            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        static string GetSourceFolderPath([CallerFilePath] string sourceFilePath = "")
        {
            string? directoryName = Path.GetDirectoryName(sourceFilePath);
            return directoryName ?? Environment.CurrentDirectory;
        }
    }
}