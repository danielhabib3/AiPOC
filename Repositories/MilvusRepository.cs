using AiPOC.Configurations;
using Milvus.Client;
using AiPOC.EmbeddingProviders;

namespace AiPOC.Repositories
{
    public abstract class MilvusRepository<T>
    {
        /// <summary>
        /// Gets data from a CSV file and returns it as a list of type T.
        /// </summary>
        /// <param name="config">The Milvus configuration containing the path to the CSV file.</param>
        /// <returns>A list of data items of type T.</returns>
        public abstract List<T> GetDataFromCsv(MilvusConfiguration config);

        /// <summary>
        /// Asynchronously creates a new collection in the Milvus database if it is not created. The provider gives the collection name and embedding dimension.
        /// </summary>
        /// <param name="client">The Milvus client instance used to connect to the database. Cannot be null.</param>
        /// <param name="provider">The embedding provider (LLM can be Gemini for example)</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public abstract Task CreateCollectionAsync(MilvusClient client, EmbeddingProvider provider);

        /// <summary>
        /// Ensures that the specified data is present in the given Milvus collection, adding any missing items as
        /// needed.
        /// </summary>
        /// <param name="collection">The Milvus collection in which to ensure the presence of the data. Cannot be null.</param>
        /// <param name="httpClient">The HTTP client used to communicate with the Milvus service. Must be properly configured for the target
        /// endpoint.</param>
        /// <param name="provider">The embedding provider used to generate embeddings for the data items. Cannot be null.</param>
        /// <param name="data">The collection of data items to ensure are present in the Milvus collection. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when all specified data items are
        /// ensured to be present in the collection.</returns>
        public abstract Task EnsureDataInCollectionAsync(MilvusCollection collection, HttpClient httpClient, EmbeddingProvider provider, IReadOnlyCollection<T> data);

        /// <summary>
        /// Asynchronously retrieves all contents from the specified Milvus collection.
        /// </summary>
        /// <param name="collection">The MilvusCollection instance representing the collection from which to retrieve contents. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of all contents of type T
        /// in the specified collection.</returns>
        public abstract Task<List<T>> GetAllContentsAsync(MilvusCollection collection);
    }
}
