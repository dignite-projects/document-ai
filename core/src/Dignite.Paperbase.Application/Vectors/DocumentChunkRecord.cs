using System;
using Microsoft.Extensions.VectorData;

namespace Dignite.Paperbase.Vectors;

// POCO describing the document-chunk shape persisted in the vector store.
// Schema conventions (storage names, the HostTenantId sentinel, Guid encoding)
// live on DocumentChunkPayloadEncoding so this type stays pure shape: attributes
// and properties only.
public sealed class DocumentChunkRecord
{
    [VectorStoreKey]
    public Guid Id { get; init; }

    [VectorStoreData(IsIndexed = true, StorageName = DocumentChunkPayloadEncoding.TenantIdStorageName)]
    public string TenantId { get; init; } = DocumentChunkPayloadEncoding.HostTenantId;

    [VectorStoreData(IsIndexed = true, StorageName = DocumentChunkPayloadEncoding.DocumentIdStorageName)]
    public string DocumentId { get; init; } = default!;

    [VectorStoreData(IsIndexed = true, StorageName = DocumentChunkPayloadEncoding.DocumentTypeCodeStorageName)]
    public string? DocumentTypeCode { get; init; }

    // ChunkIndex / Text / PageNumber are payload-only — never filtered on, so no
    // payload index is required. Keeping them indexed would cost storage with no
    // query benefit.
    [VectorStoreData(StorageName = DocumentChunkPayloadEncoding.ChunkIndexStorageName)]
    public int ChunkIndex { get; init; }

    [VectorStoreData(StorageName = DocumentChunkPayloadEncoding.TextStorageName)]
    public string Text { get; init; } = default!;

    [VectorStoreData(StorageName = DocumentChunkPayloadEncoding.PageNumberStorageName)]
    public int? PageNumber { get; init; }

    // Dimensions placeholder. The real dimension is bound at runtime by
    // DocumentChunkCollectionProvider via VectorStoreCollectionDefinition so that
    // PaperbaseAI:EmbeddingModelId can be swapped without recompiling.
    [VectorStoreVector(Dimensions: 1, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; init; }
}
