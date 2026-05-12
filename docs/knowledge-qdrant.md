# Qdrant Provider

This page documents behavior that is **specific to the `Dignite.Paperbase.KnowledgeIndex.Qdrant` provider** — things a host operator needs to know that do not generalize to other vector-store providers. The provider-neutral configuration surface lives in [`knowledge-index.md`](knowledge-index.md); guidance for authoring a non-Qdrant provider is in [`knowledge-index-provider.md`](knowledge-index-provider.md).

Currently the main Qdrant-specific topic is **hybrid search**.

## Hybrid Search

Paperbase supports **hybrid search** for Qdrant: dense-vector recall and full-text keyword recall are run in parallel and merged with Reciprocal Rank Fusion (RRF), improving precision when the query contains specific terms that a semantic embedding might dilute.

Hybrid search is opt-in and disabled by default. Pure dense-vector search remains the default.

### How It Works

When hybrid search is enabled, each call to `IDocumentKnowledgeIndex.SearchAsync` that carries a non-null `VectorSearchRequest.QueryText` executes two prefetch passes in a single Qdrant request:

| Prefetch | Query | Vector | Filter | Limit |
|---|---|---|---|---|
| Dense recall | Query embedding vector | Unnamed dense (cosine) | Base filter (tenant / document / type) | `TopK × 3` |
| Sparse BM25 recall | TF sparse vector (FNV-1a hashed terms) | `bm25` (IDF-normalized by Qdrant) | Base filter | `TopK × 3` |

Both candidate lists are fused with **Qdrant RRF** (Reciprocal Rank Fusion). The outer query applies `scoreThreshold` and `limit = TopK` to the merged results.

#### Sparse BM25 encoding

`SparseBm25Encoder` converts a query string to a sparse vector without a shared vocabulary table:

1. Tokenize with `[^\w]+` split, lowercased, minimum length 2.
2. Compute per-token TF (term frequency / total tokens).
3. Map each token to a `uint` via FNV-1a hash — deterministic across .NET versions and consistent between indexing and query time.
4. Qdrant applies IDF normalization server-side via `Modifier.Idf` on the `bm25` sparse vector field.

The same encoder runs at **index time** (inside `DocumentEmbeddingBackgroundJob`) and at **query time** whenever application code passes `VectorSearchRequest.QueryText`, so term→index mapping is always consistent.

`VectorSearchRequest.QueryText` is a caller-controlled field. The current explicit QA path sets it when building the search request. The MAF document conversation path sets it through `DocumentTextSearchAdapter.CreateSearchFunction(...)`, which passes the raw `search_paperbase_documents` tool query to Paperbase RAG.

### Prerequisites

- **Qdrant ≥ 1.10** — RRF fusion via the Query API was introduced in Qdrant 1.10. Sparse vector support (`SparseVectorParams`, `Modifier.Idf`) requires Qdrant ≥ 1.7.
- **`EnsureCollectionOnStartup: true`** — required so `QdrantClientGateway.EnsureCollectionAsync` declares the `bm25` sparse vector when **creating** the collection.

#### Important: enabling hybrid search on an existing collection requires a rebuild

Qdrant's `UpdateCollection` API **cannot add a new sparse vector field** to an existing collection — it can only modify parameters of a sparse vector that was declared at creation time. Attempting to update a dense-only collection with a `bm25` sparse vector config returns:

```
Status(StatusCode="InvalidArgument", Detail="Wrong input: Not existing vector name error: bm25")
```

`QdrantClientGateway.EnsureSparseBm25VectorAsync` detects this case at startup and throws a clear `InvalidOperationException` instructing the operator to drop and recreate the collection.

**This means hybrid search must be decided before the collection is first created.** Switching `EnableHybridSearch` from `false` to `true` on a live deployment requires:

1. Drop the Qdrant collection (e.g. `DELETE /collections/paperbase_document_chunks` against the HTTP port `6333`).
2. Restart the host. Startup will recreate the collection with the `bm25` sparse vector declared (the `CreateCollection` branch).
3. Re-index every existing document. The application does not currently expose a "rebuild all" admin endpoint; an operator-side script must enqueue `DocumentEmbeddingJobArgs` for each document, or each document must be re-uploaded.

Switching from `true` to `false` is safe: the leftover sparse vectors on existing points become dead storage but cause no errors, and queries take the dense-only path automatically.

### Enabling Hybrid Search

Set the following key in `appsettings.json` (or environment variable):

```json
"QdrantKnowledgeIndex": {
  "EnableHybridSearch": true
}
```

No code changes are needed. The `QdrantDocumentKnowledgeIndex` picks up the option at startup.

#### Complete Qdrant section after enabling

```json
"QdrantKnowledgeIndex": {
  "Endpoint": "http://localhost:6334",
  "ApiKey": "",
  "CollectionName": "paperbase_document_chunks",
  "Distance": "Cosine",
  "VectorDimension": 1536,
  "EnsureCollectionOnStartup": true,
  "EnableHybridSearch": true
}
```

### Score Semantics Under RRF

RRF scores are not comparable to cosine-similarity scores — they rank candidates relative to each other in a small positive range (typically `0.01–0.065`), not normalized relevance probabilities.

The Qdrant provider hides this from callers: on the hybrid path it returns `VectorSearchResult.Score = null`, signalling that no normalized score is available. `MinScore` thresholds (cosine-scale) are skipped automatically and ranking is preserved through `TopK`. **No configuration change is required when toggling `EnableHybridSearch`.**

### Disabling Hybrid Search on a Per-Request Basis

Set `VectorSearchRequest.QueryText = null` before calling `IDocumentKnowledgeIndex.SearchAsync` to force dense-only search regardless of the `EnableHybridSearch` flag. This is useful for similarity lookups where a free-form text query is not available.

### Fallback for Non-Qdrant Providers

`VectorSearchRequest.QueryText` is a provider-neutral field defined in `Dignite.Paperbase.KnowledgeIndex`. Providers that do not support hybrid search ignore it and perform pure dense-vector search. No configuration change is needed on providers that do not implement `QueryHybridAsync`. See [`knowledge-index-provider.md`](knowledge-index-provider.md) for how to add a new provider.
