# FaqWithMilvus

**Namespace:** `AiPOC.FAQ.WithMilvus`  
**File:** `FaqWithMilvus.cs`

## Overview

`FaqWithMilvus` is an interactive console application that performs **semantic FAQ search** using [Milvus](https://milvus.io/) as the vector database. It supports four embedding providers and lets the user search FAQ entries by question, answer, or a combined question+answer vector, ranking results by L2 distance.

---

## Dependencies

| Package / Namespace | Purpose |
|---|---|
| `Milvus.Client` | Milvus vector database client |
| `AiPOC.EmbeddingProviders` | Abstraction over embedding APIs (Gemini, Claude, Mistral, OpenAI) |
| `AiPOC.Milvus` | Milvus utilities (retry logic, field extraction) |
| `AiPOC.Repositories` | FAQ data access and collection management |
| `AiPOC.Configurations` | Strongly-typed app configuration |

---

## Architecture & Flow

```
RunFaqWithMilvus()
│
├── ReadEmbeddingProvider()        ← user selects AI provider
│
├── MilvusClient                   ← connect to Milvus
├── faqRepository.CreateCollectionAsync()
├── faqRepository.EnsureDataInCollectionAsync()
├── PrintAllFaqsAsync()            ← display all stored FAQs
│
└── Search loop
    ├── User picks search mode (question / answer / combined)
    ├── provider.GetEmbeddingAsync()   ← embed the query
    └── collection.SearchAsync()       ← ANN search (L2)
        └── Print ranked results
```

---

## Methods

### `RunFaqWithMilvus` (public, static, async)

```csharp
public static async Task RunFaqWithMilvus(
    EmbeddingServiceConfiguration embeddingConfig,
    FaqRepository faqRepository)
```

Main entry point. Runs two nested loops:

- **Outer loop** — lets the user switch embedding provider without restarting the app.
- **Inner loop** — presents the search menu and handles individual queries.

**Parameters**

| Parameter | Type | Description |
|---|---|---|
| `embeddingConfig` | `EmbeddingServiceConfiguration` | API keys/endpoints for all providers and Milvus connection settings |
| `faqRepository` | `FaqRepository` | Handles collection creation, CSV loading, data insertion, and field-name constants |

**Inner loop menu options**

| Choice | Action |
|---|---|
| `1` | Search by **question** vector |
| `2` | Search by **answer** vector |
| `3` | Search by **combined Q+A** vector |
| `4` | Return to provider selection |
| `0` | Exit the application |

**Search behaviour**

1. Reads a free-text query from the console.
2. Calls `provider.GetEmbeddingAsync()` to obtain a float vector.
3. Executes `collection.SearchAsync()` with `SimilarityMetricType.L2` against all stored entities.
4. Sorts results ascending by L2 distance (smallest = most similar) and prints each FAQ with its distance score.

---

### `ReadEmbeddingProvider` (private, static)

```csharp
static EmbeddingProvider ReadEmbeddingProvider(EmbeddingServiceConfiguration config)
```

Prompts the user to choose an embedding provider and returns the corresponding provider instance. Loops until a valid choice is entered.

**Supported providers**

| Choice | Provider class | Underlying API |
|---|---|---|
| `1` | `GeminiEmbeddingProvider` | Google Gemini |
| `2` | `ClaudeEmbeddingProvider` | Anthropic / VoyageAI |
| `3` | `MistralEmbeddingProvider` | Mistral AI |
| `4` | `OpenAiEmbeddingProvider` | OpenAI |

**Returns:** A configured `EmbeddingProvider` instance ready for embedding generation.

---

### `PrintAllFaqsAsync` (private, static, async)

```csharp
static async Task PrintAllFaqsAsync(MilvusCollection collection, FaqRepository faqRepository)
```

Fetches and displays every FAQ entry currently stored in the collection. Called once after data ingestion so the user can see what is available before searching.

**Parameters**

| Parameter | Type | Description |
|---|---|---|
| `collection` | `MilvusCollection` | The active Milvus collection to query |
| `faqRepository` | `FaqRepository` | Provides field-name constants for the query |

**Details**

- Uses a `QueryAsync("id >= 0")` expression to retrieve all rows (up to the Milvus maximum of **16 384** per call).
- Outputs `(aucun contenu)` if the collection is empty.

---

## Search Vector Fields

The vector field used for a search is resolved from `FaqRepository`:

| Field constant | Represents |
|---|---|
| `FieldQuestionVector` | Embedding of the FAQ question only |
| `FieldAnswerVector` | Embedding of the FAQ answer only |
| `FieldQaVector` | Embedding of the concatenated question + answer |

---

## Notes

- **Rate-limit resilience:** All Milvus calls are wrapped in `MilvusUtils.ExecuteWithRateLimitRetryAsync()`, which automatically retries on rate-limit errors.
- **Result ranking:** L2 distance is used as the similarity metric. A lower score means higher similarity. Results are always sorted ascending before display.
- **Search limit:** The search limit is dynamically set to the current entity count (`Math.Max(1, entityCount)`), ensuring every stored FAQ is ranked in the result set.