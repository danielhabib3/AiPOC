using AiPOC.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AiPOC.FAQ.WithMilvus;
using AiPOC.Repositories;
using AiPOC.Suggestion.WithMilvus;
using AiPOC.Suggestion.WithoutMilvus;

namespace AiPOC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var embeddingConfig = builder.Configuration
                .GetSection("EmbeddingService")
                .Get<EmbeddingServiceConfiguration>() ?? throw new Exception("Failed to load EmbeddingService configuration.");

            RunSwaggerUI(args, embeddingConfig, builder);

            //if (args.Length > 0)
            //{
            //    var mode = args[0].ToLower();
            //    switch (mode)
            //    {
            //        case "swagger":
            //            RunSwaggerUI(args, embeddingConfig, builder);
            //            break;
            //        case "faq-milvus":
            //            RunFaqWithMilvusInConsole(args, embeddingConfig, builder).GetAwaiter().GetResult();
            //            break;
            //        case "suggestion-milvus":
            //            RunSuggestionWithMilvusInConsole(args, embeddingConfig, builder).GetAwaiter().GetResult();
            //            break;
            //        case "suggestion-without-milvus":
            //            RunSuggestionWithoutMilvusInConsole(args, embeddingConfig, builder).GetAwaiter().GetResult();
            //            break;
            //        default:
            //            Console.WriteLine("Invalid mode specified. Use 'swagger', 'faq-milvus', 'suggestion-milvus', or 'suggestion-without-milvus'.");
            //            break;
            //    }
            //}
            //else
            //{
            //    Console.WriteLine("No mode specified. Use 'swagger', 'faq-milvus', 'suggestion-milvus', or 'suggestion-without-milvus' as a command-line argument.");
            //}
        }

        private static void RunSwaggerUI(string[] args, EmbeddingServiceConfiguration embeddingConfig, WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton(embeddingConfig);

            builder.Services.AddControllers();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }

        private static async Task RunFaqWithMilvusInConsole(string[] args, EmbeddingServiceConfiguration embeddingConfig, WebApplicationBuilder builder)
        {
            await FaqWithMilvus.RunFaqWithMilvus(embeddingConfig, new FaqRepository());
        }

        private static async Task RunSuggestionWithMilvusInConsole(string[] args, EmbeddingServiceConfiguration embeddingConfig, WebApplicationBuilder builder)
        {
            await SuggestionWithMilvus.RunSuggestionWithMilvus(embeddingConfig, new SuggestionRepository());
        }

        private static async Task RunSuggestionWithoutMilvusInConsole(string[] args, EmbeddingServiceConfiguration embeddingConfig, WebApplicationBuilder builder)
        {
            await SuggestionWithoutMilvus.RunSuggestionWithoutMilvus(embeddingConfig);
        }
    }
}