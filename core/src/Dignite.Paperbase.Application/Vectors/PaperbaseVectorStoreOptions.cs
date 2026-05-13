namespace Dignite.Paperbase.Vectors;

/// <summary>
/// Configuration for the document-chunk vector store. Three concern groups colocated
/// in one section so operators see "how vector retrieval is wired" in one place:
/// <list type="bullet">
///   <item><b>Schema</b> (<see cref="CollectionName"/>, <see cref="EmbeddingDimension"/>)
///         — identity and dimension of the Qdrant collection. These are read at
///         host startup; changes require a restart and may require a collection
///         rotation. See class comment on <c>DocumentChunkCollectionProvider</c>.</item>
///   <item><b>Retrieval</b> (<see cref="DefaultTopK"/>, <see cref="MinScore"/>)
///         — knobs applied per search call. Per-request scope overrides may take
///         precedence (see <c>DocumentSearchScope</c>).</item>
///   <item><b>Cleanup</b> (<see cref="CleanupPageSize"/>, <see cref="CleanupMaxIterations"/>)
///         — pagination bounds for the list-then-delete cascade-delete and stale-chunk
///         prune paths. These do not cap the total number of chunks cleanable;
///         the cleanup loop pages until exhausted.</item>
/// </list>
/// Qdrant connection parameters (endpoint, API key) live separately under the
/// <c>PaperbaseVectorStore:Qdrant</c> sub-section and are read directly by the host
/// module, not by this options class — they're connector-specific wiring rather
/// than application behavior.
/// </summary>
public class PaperbaseVectorStoreOptions
{
    // ── Schema ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Qdrant collection name backing <see cref="DocumentChunkRecord"/>.
    /// Change the name + re-run the embedding job when rotating the embedding model.
    /// </summary>
    public string CollectionName { get; set; } = "paperbase_document_chunks";

    /// <summary>
    /// Embedding vector dimension. Must match the embedding model registered via
    /// <c>PaperbaseAI:EmbeddingModelId</c>. Changing this requires rebuilding the
    /// collection by re-running the embedding job for all documents.
    /// </summary>
    public int EmbeddingDimension { get; set; } = 1536;

    // ── Retrieval ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Default number of top results when callers don't override.
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Minimum acceptable cosine similarity in [0, 1]. <c>null</c> disables the threshold.
    /// </summary>
    public double? MinScore { get; set; } = 0.65;

    // ── Cleanup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Page size for the list-then-delete cleanup paths (cascade delete on document
    /// deletion, stale-chunk pruning on re-embed). The cascade-delete handler pages
    /// through (filter + DeleteAsync) until the page returns empty, so this only
    /// caps the working set held in memory per round-trip — not the total number of
    /// chunks that can be cleaned up.
    /// </summary>
    public int CleanupPageSize { get; set; } = 1_000;

    /// <summary>
    /// Maximum cleanup iterations before the cascade-delete loop bails out and logs a
    /// warning. Bounds runaway loops if the backing store somehow keeps returning the
    /// same records (eventual consistency edge cases). 100 iterations × 1000 page size
    /// = 100K chunks per document, well above any realistic single-document size.
    /// </summary>
    public int CleanupMaxIterations { get; set; } = 100;
}
