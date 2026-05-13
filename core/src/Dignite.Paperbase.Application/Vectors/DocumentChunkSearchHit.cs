using System;

namespace Dignite.Paperbase.Vectors;

// Application-layer projection of a chat-retrieval hit. Insulates DocumentSearchCapture
// + ChatAppService.SerializeCitations + DocumentRerankWorkflow from MEVD's generic
// VectorSearchResult<TRecord> so the chat path stays free of vector-store-specific
// types, and the storage form (DocumentChunkRecord.DocumentId as string) is parsed
// to a typed Guid once at the adapter boundary instead of being re-parsed in every
// downstream caller.
//
// Why not just consume VectorSearchResult<DocumentChunkRecord> directly?
//   1. Type pollution. Capture / ChatAppService / RerankCandidate.Tag would all gain
//      a transitive dependency on Microsoft.Extensions.VectorData generics, which
//      bleeds the storage-layer concept into the chat / citation surface.
//   2. DocumentChunkRecord.DocumentId is a `string` (D-format Guid for on-disk
//      compatibility); downstream code wants a typed Guid. Mapping once at the
//      adapter boundary localizes the parse + null-handling.
//   3. The two types diverge: Hit carries Score at the top level (vs nested in
//      VectorSearchResult), and over time it will gain UI-shaped fields the
//      storage record won't (e.g. highlight ranges).
//
// Trade-off accepted: this DTO duplicates DocumentChunkRecord fields. When adding
// a new chunk attribute that needs to surface in citations, update both this record
// and DocumentTextSearchAdapter.MapToHit. Do not collapse the two into one type —
// the indirection is intentional.
public sealed record DocumentChunkSearchHit
{
    public required Guid Id { get; init; }
    public required Guid DocumentId { get; init; }
    public string? DocumentTypeCode { get; init; }
    public required int ChunkIndex { get; init; }
    public required string Text { get; init; }
    public int? PageNumber { get; init; }
    public double? Score { get; init; }
}
