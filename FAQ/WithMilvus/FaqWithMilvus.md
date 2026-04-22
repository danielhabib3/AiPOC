# FAQWithMilvus — Technical Documentation

## Overview

`FAQWithMilvus` is a C# console application that loads FAQ entries from a CSV file, generates vector embeddings using one of four AI providers (Gemini, VoyageAI/Claude, Mistral, OpenAI), stores them in a [Milvus](https://milvus.io/) vector database, and exposes an interactive semantic search interface.

Each FAQ entry is embedded three ways — by question, by answer, and by their combination — enabling flexible similarity search across all three axes.

---

## Table of Contents

1. [Architecture](#architecture)
2. [Dependencies](#dependencies)
3. [Configuration](#configuration)
   - [API Keys](#api-keys)
   - [Milvus Ports](#milvus-ports)
   - [Embedding Dimensions](#embedding-dimensions)
4. [Data Model](#data-model)
   - [Faq Class](#faq-class)
   - [Milvus Collection Schema](#milvus-collection-schema)
5. [Embedding Providers](#embedding-providers)
6. [Core Workflow](#core-workflow)
   - [Entry Point](#entry-point)
   - [Provider Selection](#provider-selection)
   - [Collection Initialization](#collection-initialization)
   - [FAQ Seeding](#faq-seeding)
   - [Interactive Search Menu](#interactive-search-menu)
7. [Methods Reference](#methods-reference)
8. [Error Handling & Retry Logic](#error-handling--retry-logic)
9. [Extending the Application](#extending-the-application)

---

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                        Console App                           │
│                                                              │
│  faq_washme.csv ──► CsvHelper ──► List<Faq>                 │
│                                         │                    │
│                                    Embedding                 │
│                                    Provider                  │
│                              ┌─────────┴──────────┐         │
│                           Gemini  Claude  Mistral  OpenAI    │
│                              └─────────┬──────────┘         │
│                                   float[]                    │
│                                        │                     │
│              ┌─────────────────────────▼──────────────────┐ │
│              │              Milvus (per-provider port)     │ │
│              │  id | question | answer | question_vector   │ │
│              │              | answer_vector | qa_vector    │ │
│              └────────────────────────────────────────────┘ │
│                                        │                     │
│                              Search Query ──► Ranked Results │
└──────────────────────────────────────────────────────────────┘
```

---

## Dependencies

| Package | Purpose |
|---|---|
| `CsvHelper` | Parsing `faq_washme.csv` |
| `Milvus.Client` | Communicating with the Milvus vector database |
| `System.Text.Json` | Serializing/deserializing embedding API payloads |
| `System.Net.Http` | Making HTTP calls to embedding providers |

---

## Configuration

All configuration constants are defined as `private const` fields at the top of `FAQWithMilvus`.

### API Keys

| Constant | Provider | Default value |
|---|---|---|
| `GeminiApiKey` | Google Gemini | `"AIzaSy..."` |
| `VoyageApiKey` | VoyageAI (Claude embeddings) | `"pa-wQ..."` |
| `MistralApiKey` | Mistral AI | `"Fh4sQH..."` |
| `OpenAiApiKey` | OpenAI | `"MY_API_KEY"` |

> **Security note:** API keys are hardcoded in the source. For production use, load them from environment variables or a secrets manager.

### Milvus Ports

Each provider stores its embeddings in a **separate Milvus instance** (different port), preventing dimension conflicts between providers.

| Provider | Port |
|---|---|
| Gemini | `19532` |
| Claude (VoyageAI) | `19531` |
| Mistral | `19533` |
| OpenAI | `19534` |

All instances run on `127.0.0.1`.

### Embedding Dimensions

| Provider | Model | Dimension |
|---|---|---|
| Gemini | `gemini-embedding-001` | 3072 |
| Claude / VoyageAI | `voyage-3.5` | 1024 |
| Mistral | `mistral-embed` | 1024 |
| OpenAI | `text-embedding-3-large` | 3072 |

---

## Data Model

### Faq Class

```csharp
public class Faq
{
    public string Question { get; set; } = string.Empty;
    public string Answer   { get; set; } = string.Empty;
}
```

Loaded from `faq_washme.csv` using `;` as the delimiter with a header row.

### Milvus Collection Schema

Each provider gets its own collection named `faqs_<provider>` (e.g. `faqs_gemini`).

| Field | Type | Description |
|---|---|---|
| `id` | `long` | Auto-generated primary key |
| `question` | `varchar(1024)` | Raw FAQ question text |
| `answer` | `varchar(4096)` | Raw FAQ answer text |
| `question_vector` | `float_vector(D)` | Embedding of the question only |
| `answer_vector` | `float_vector(D)` | Embedding of the answer only |
| `qa_vector` | `float_vector(D)` | Embedding of `"question answer"` (concatenated) |

`D` is the dimension for the selected provider (see [Embedding Dimensions](#embedding-dimensions)).

An `AutoIndex` with `L2` metric type is created on all three vector fields at collection creation time.

---

## Embedding Providers

The application abstracts the embedding call behind a delegate:

```csharp
Func<HttpClient, string, Task<List<float>?>> embeddingFunc
```

### Gemini

- **Endpoint:** `https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent`
- **Auth:** `x-goog-api-key` header
- **Task type:** `SEMANTIC_SIMILARITY`
- **Response path:** `embedding.values[]`

### VoyageAI (Claude)

- **Endpoint:** `https://api.voyageai.com/v1/embeddings`
- **Auth:** `Authorization: Bearer <key>`
- **Model:** `voyage-3.5`
- **Response path:** `data[0].embedding[]`

### Mistral

- **Endpoint:** `https://api.mistral.ai/v1/embeddings`
- **Auth:** `Authorization: Bearer <key>`
- **Model:** `mistral-embed`
- **Response path:** `data[0].embedding[]`

### OpenAI

- **Endpoint:** `https://api.openai.com/v1/embeddings`
- **Auth:** `Authorization: Bearer <key>`
- **Model:** `text-embedding-3-large`
- **Response path:** `data[0].embedding[]`

---

## Core Workflow

### Entry Point

`RunFAQWithMilvus()` is the main async method. It runs an outer loop that allows switching providers, and an inner loop for the interactive search menu.

```
RunFAQWithMilvus()
 └── ReadEmbeddingProvider()           // select AI provider
 └── Connect to Milvus
 └── CreateCollectionAsync()           // if not exists
     └── CreateIndexAsync()            // on all 3 vector fields
 └── Load faq_washme.csv
 └── EnsureFaqsInCollectionAsync()     // seed missing entries
 └── PrintAllFaqsAsync()               // display all FAQs
 └── Interactive menu loop
     └── SearchAsync()                 // semantic search
```

### Provider Selection

`ReadEmbeddingProvider()` loops until a valid choice (`1`–`4`) is entered. Returns an `EmbeddingProvider` enum value.

```
=== CHOIX IA ===
1. Gemini  (actif)
2. Claude  (VoyageAI)
3. Mistral
4. OpenAI
```

### Collection Initialization

If the collection does not yet exist in Milvus:
1. The schema (6 fields) is created.
2. `AutoIndex` (L2) indexes are built on `question_vector`, `answer_vector`, and `qa_vector`.

### FAQ Seeding

`EnsureFaqsInCollectionAsync()` performs an **idempotent upsert**:

1. Loads all existing questions from Milvus into a `HashSet<string>` (case-insensitive).
2. Filters the CSV list to entries **not** already present.
3. For each missing FAQ, generates three embeddings (Q, A, Q+A) and inserts a row.
4. Flushes and reloads the collection.

This ensures the same CSV can be run multiple times without duplicating data.

### Interactive Search Menu

```
=== MENU FAQ ===
1. Rechercher par question          → searches question_vector
2. Rechercher par réponse           → searches answer_vector
3. Rechercher (question + réponse)  → searches qa_vector
4. Retour (changer de fournisseur)
0. Quitter
```

For a given query string:
1. The query is embedded using the active provider.
2. `SearchAsync()` is called against the selected vector field with L2 metric.
3. Results are ranked by ascending L2 distance (closer = more similar).
4. All matching FAQs are printed with their distance score.

---

## Methods Reference

### `RunFAQWithMilvus()`
Main entry point. Orchestrates provider selection, collection setup, seeding, and the search menu loop.

### `ReadEmbeddingProvider()` → `EmbeddingProvider`
Prompts the user to select an embedding provider. Loops on invalid input.

### `EnsureFaqsInCollectionAsync(collection, httpClient, embeddingFunc, faqs)`
Idempotent seeding method. Inserts only FAQ entries absent from the collection. Generates all three vector representations per entry.

### `GetAllQuestionsAsync(collection)` → `List<string>`
Queries Milvus for all existing `question` field values (up to 16 384 rows).

### `PrintAllFaqsAsync(collection)`
Fetches and prints all FAQ question/answer pairs stored in the collection.

### `ExtractFieldValues(fieldsData, targetField)` → `List<string>`
Reflection-based helper that extracts string values from a Milvus `FieldData` list by field name. Handles both `IEnumerable<string>` and `IEnumerable<object>` data shapes.

### `GetEmbeddingGeminiAsync(client, text)` → `List<float>?`
Calls the Gemini embedding API. Returns `null` on failure.

### `GetEmbeddingClaudeAsync(client, text)` → `List<float>?`
Calls the VoyageAI embedding API. Returns `null` on failure.

### `GetEmbeddingMistralAsync(client, text)` → `List<float>?`
Calls the Mistral embedding API. Returns `null` on failure.

### `GetEmbeddingOpenAiAsync(client, text)` → `List<float>?`
Calls the OpenAI embedding API. Returns `null` on failure.

### `ExecuteWithRateLimitRetryAsync(action, maxRetries = 4)`
Generic retry wrapper for Milvus operations. Catches `MilvusException` on transient errors and applies **exponential backoff** (capped at 30 s).

### `IsTransientMilvusError(ex)` → `bool`
Returns `true` if the exception message contains any of: `RateLimit`, `rate limit`, `NoReplicaAvailable`, `channel not available`.

### `GetMilvusPort(provider)` → `int`
Maps `EmbeddingProvider` → Milvus port number.

### `GetEmbeddingDimension(provider)` → `int`
Maps `EmbeddingProvider` → vector dimension.

### `GetCollectionName(provider)` → `string`
Maps `EmbeddingProvider` → Milvus collection name (`faqs_gemini`, `faqs_claude`, `faqs_mistral`, `faqs_openai`).

---

## Error Handling & Retry Logic

All Milvus calls are wrapped in `ExecuteWithRateLimitRetryAsync`. The retry strategy is:

| Attempt | Wait before retry |
|---|---|
| 1st | 10 s |
| 2nd | 20 s |
| 3rd | 30 s (capped) |
| 4th | 30 s (capped) |

After `maxRetries` (default 4) failed attempts, the exception propagates.

Embedding API failures return `null` and are logged to the console; the corresponding FAQ entry is skipped during seeding, and the search is aborted with an informational message.

---

## Extending the Application

### Adding a new embedding provider

1. Add a new value to the `EmbeddingProvider` enum.
2. Implement a `GetEmbeddingXxxAsync(HttpClient, string)` method.
3. Add a new `MilvusPortXxx` and `XxxEmbeddingDimension` constant.
4. Wire the new provider into `ReadEmbeddingProvider()`, the `embeddingFunc` switch, `GetMilvusPort()`, `GetEmbeddingDimension()`, and `GetCollectionName()`.

### Switching similarity metric

Replace `SimilarityMetricType.L2` with `SimilarityMetricType.IP` (inner product / cosine) in both `CreateIndexAsync` and `SearchAsync` calls. Make sure embeddings are normalized if using inner product.

### Loading FAQs from a different source

Replace the `CsvReader` block in `RunFAQWithMilvus()` with any `List<Faq>` construction (database query, REST API, JSON file, etc.) — the rest of the pipeline is source-agnostic.