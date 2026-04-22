# SuggestionWithMilvus

**Namespace:** `AiPOC.Suggestion.WithMilvus`  
**File:** `SuggestionWithMilvus.cs`

## Overview

`SuggestionWithMilvus` is an interactive console application that performs **semantic text suggestion search** using [Milvus](https://milvus.io/) as the vector database. Given a free-text input, it embeds the query and retrieves the most similar stored suggestions ranked by L2 distance. It supports four embedding providers (Gemini, Claude/VoyageAI, Mistral, OpenAI).

---

## Dependencies

| Package / Namespace | Purpose |
|---|---|
| `Milvus.Client` | Milvus vector database client |
| `AiPOC.EmbeddingProviders` | Abstraction over embedding APIs (Gemini, Claude, Mistral, OpenAI) |
| `AiPOC.Milvus` | Milvus utilities (retry logic, field extraction) |
| `AiPOC.Repositories` | Suggestion data access and collection management |
| `AiPOC.Configurations` | Strongly-typed app configuration |

---

## Architecture & Flow

```
RunSuggestionWithMilvus()
│
├── ReadEmbeddingProvider()              ← user selects AI provider
│
├── MilvusClient                         ← connect to Milvus
├── suggestionRepository.CreateCollectionAsync()
├── suggestionRepository.EnsureDataInCollectionAsync()
├── PrintAllContentsAsync()              ← display all stored suggestions
│
└── Search loop
    ├── User enters a free-text query
    ├── provider.GetEmbeddingAsync()     ← embed the query
    └── collection.SearchAsync()         ← ANN search (L2) on "embedding" field
        └── Print ranked results
```

---

## Methods

### `RunSuggestionWithMilvus` (public, static, async)

```csharp
public static async Task RunSuggestionWithMilvus(
    EmbeddingServiceConfiguration embeddingConfig,
    SuggestionRepository suggestionRepository)
```

Main entry point. Runs two nested loops:

- **Outer loop** — lets the user switch embedding provider without restarting the app.
- **Inner loop** — presents the search menu and processes individual queries.

**Parameters**

| Parameter | Type | Description |
|---|---|---|
| `embeddingConfig` | `EmbeddingServiceConfiguration` | API keys/endpoints for all providers and Milvus connection settings |
| `suggestionRepository` | `SuggestionRepository` | Handles collection creation, CSV loading, data insertion, and content retrieval |

**Inner loop menu options**

| Choice | Action |
|---|---|
| `1` | Search for similar suggestions by text |
| `2` | Return to provider selection |
| `0` | Exit the application |

**Search behaviour**

1. Reads a free-text query from the console.
2. Calls `provider.GetEmbeddingAsync()` to obtain a float vector.
3. Executes `collection.SearchAsync()` against the `embedding` field with `SimilarityMetricType.L2`.
4. Sorts results ascending by L2 distance (smallest = most similar) and prints each suggestion with its score.

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

Each provider is instantiated with `config.Milvus.SuggestionCollectionPrefix` to scope its Milvus collection separately from other features (e.g. FAQ).

**Returns:** A configured `EmbeddingProvider` instance ready for embedding generation.

---

### `PrintAllContentsAsync` (private, static, async)

```csharp
static async Task PrintAllContentsAsync(MilvusCollection collection, SuggestionRepository suggestionRepository)
```

Fetches and displays every suggestion text currently stored in the collection. Called once after data ingestion so the user can review available content before searching.

**Parameters**

| Parameter | Type | Description |
|---|---|---|
| `collection` | `MilvusCollection` | The active Milvus collection to query |
| `suggestionRepository` | `SuggestionRepository` | Delegates the actual fetch via `GetAllContentsAsync()` |

**Details**

- Delegates retrieval to `suggestionRepository.GetAllContentsAsync(collection)`.
- Prints `(aucun contenu)` if no records are found.

---

## Vector Field

Unlike `FaqWithMilvus` which supports multiple vector fields, this class always searches a single field:

| Field name | Description |
|---|---|
| `embedding` | Vector representation of the suggestion text |
| `content` | Raw suggestion text, returned as an output field |

---

## Comparison with FaqWithMilvus

| Feature | `SuggestionWithMilvus` | `FaqWithMilvus` |
|---|---|---|
| Vector fields | Single (`embedding`) | Three (`question`, `answer`, `Q+A`) |
| Search modes | 1 (free text) | 3 (by field type) |
| Repository | `SuggestionRepository` | `FaqRepository` |
| Collection prefix | `SuggestionCollectionPrefix` | `FaqCollectionPrefix` |
| Content display | Delegated to repository | Implemented inline |

---

## Notes

- **Rate-limit resilience:** `collection.SearchAsync()` is wrapped in `MilvusUtils.ExecuteWithRateLimitRetryAsync()`, which automatically retries on rate-limit errors.
- **Result ranking:** L2 distance is used as the similarity metric. A lower score means higher similarity. Results are always sorted ascending before display.
- **Search limit:** Dynamically set to the current entity count (`Math.Max(1, entityCount)`), ensuring every stored suggestion is included in the ranking.