using Milvus.Client;
using System.Reflection;

namespace AiPOC.Milvus
{
    public static class MilvusUtils
    {
        public static List<string> ExtractFieldValues(IReadOnlyList<FieldData> fieldsData, string targetField)
        {
            var values = new List<string>();

            foreach (var field in fieldsData)
            {
                Type t = field.GetType();
                PropertyInfo? nameProp = t.GetProperty("FieldName") ?? t.GetProperty("Name");
                string? fieldName = nameProp?.GetValue(field) as string;

                if (!string.Equals(fieldName, targetField, StringComparison.OrdinalIgnoreCase)) continue;

                PropertyInfo? dataProp = t.GetProperty("Data") ?? t.GetProperty("Values");
                object? rawData = dataProp?.GetValue(field);

                if (rawData is IEnumerable<string> stringEnumerable)
                {
                    values.AddRange(stringEnumerable.Where(s => !string.IsNullOrWhiteSpace(s)));
                    continue;
                }

                if (rawData is IEnumerable<object> objectEnumerable)
                {
                    values.AddRange(objectEnumerable.Select(o => o?.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!));
                }
            }

            return values;
        }

        public static async Task ExecuteWithRateLimitRetryAsync(Func<Task> action, int maxRetries = 4)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { await action(); return; }
                catch (MilvusException ex) when (IsTransientMilvusError(ex) && attempt < maxRetries)
                {
                    int wait = (int)Math.Min(30, Math.Pow(2, attempt) * 10);
                    Console.WriteLine($"Milvus temporairement indisponible, tentative dans {wait}s...");
                    await Task.Delay(TimeSpan.FromSeconds(wait));
                }
            }
        }

        public static async Task<T> ExecuteWithRateLimitRetryAsync<T>(Func<Task<T>> action, int maxRetries = 4)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return await action(); }
                catch (MilvusException ex) when (IsTransientMilvusError(ex) && attempt < maxRetries)
                {
                    int wait = (int)Math.Min(30, Math.Pow(2, attempt) * 10);
                    Console.WriteLine($"Milvus temporairement indisponible, tentative dans {wait}s...");
                    await Task.Delay(TimeSpan.FromSeconds(wait));
                }
            }
        }

        public static bool IsTransientMilvusError(MilvusException ex) =>
            ex.Message.Contains("RateLimit", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("NoReplicaAvailable", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("channel not available", StringComparison.OrdinalIgnoreCase);
    }
}
