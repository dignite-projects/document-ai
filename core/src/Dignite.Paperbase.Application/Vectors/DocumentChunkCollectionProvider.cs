using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Volo.Abp.DependencyInjection;

namespace Dignite.Paperbase.Vectors;

// Resolves the project's single DocumentChunkRecord VectorStoreCollection,
// ensuring it exists on first use. Wraps GetCollection + EnsureCollectionExistsAsync
// so callers don't have to remember to call EnsureCollectionExistsAsync themselves.
//
// The runtime embedding dimension flows in via PaperbaseVectorStoreOptions and
// overrides the attribute placeholder on DocumentChunkRecord.Embedding — MEVD
// honors the dimension supplied via VectorStoreCollectionDefinition when present,
// which lets PaperbaseAI:EmbeddingModelId be swapped without recompiling.
//
// Lifecycle / hot-reload semantics
// --------------------------------
// Singleton with a captured `_options` snapshot taken at construction. Changes to
// PaperbaseVectorStoreOptions at runtime (e.g. via an IOptionsMonitor reload) are
// NOT picked up — `_cached` holds the collection bound to the original CollectionName
// + EmbeddingDimension and stays alive for the host's lifetime. This is intentional:
//   - CollectionName drives Qdrant collection identity; switching it mid-run would
//     orphan in-flight upserts.
//   - EmbeddingDimension is dimension-locked on the Qdrant collection; changing it
//     requires the operator to either rotate CollectionName or drop the collection
//     and re-run the embedding job. There is no in-place dimension change.
// Operators that change these settings must restart the host.
public class DocumentChunkCollectionProvider : ISingletonDependency
{
    private readonly VectorStore _vectorStore;
    private readonly PaperbaseVectorStoreOptions _options;
    private VectorStoreCollection<Guid, DocumentChunkRecord>? _cached;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);

    public DocumentChunkCollectionProvider(
        VectorStore vectorStore,
        IOptions<PaperbaseVectorStoreOptions> options)
    {
        _vectorStore = vectorStore;
        // Snapshot at construction — see hot-reload comment on the class.
        _options = options.Value;
    }

    public virtual async Task<VectorStoreCollection<Guid, DocumentChunkRecord>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached != null)
            return _cached;

        await _ensureLock.WaitAsync(cancellationToken);
        try
        {
            if (_cached != null)
                return _cached;

            var collection = _vectorStore.GetCollection<Guid, DocumentChunkRecord>(
                _options.CollectionName,
                BuildDefinition(_options.EmbeddingDimension));

            await collection.EnsureCollectionExistsAsync(cancellationToken);
            _cached = collection;
            return collection;
        }
        finally
        {
            _ensureLock.Release();
        }
    }

    protected virtual VectorStoreCollectionDefinition BuildDefinition(int dimensions)
    {
        return new VectorStoreCollectionDefinition
        {
            Properties =
            {
                new VectorStoreKeyProperty(nameof(DocumentChunkRecord.Id), typeof(Guid)),
                new VectorStoreDataProperty(nameof(DocumentChunkRecord.TenantId), typeof(string))
                {
                    IsIndexed = true,
                    StorageName = DocumentChunkPayloadEncoding.TenantIdStorageName,
                },
                new VectorStoreDataProperty(nameof(DocumentChunkRecord.DocumentId), typeof(string))
                {
                    IsIndexed = true,
                    StorageName = DocumentChunkPayloadEncoding.DocumentIdStorageName,
                },
                new VectorStoreDataProperty(nameof(DocumentChunkRecord.DocumentTypeCode), typeof(string))
                {
                    IsIndexed = true,
                    StorageName = DocumentChunkPayloadEncoding.DocumentTypeCodeStorageName,
                },
                new VectorStoreDataProperty(nameof(DocumentChunkRecord.ChunkIndex), typeof(int))
                {
                    StorageName = DocumentChunkPayloadEncoding.ChunkIndexStorageName,
                },
                new VectorStoreDataProperty(nameof(DocumentChunkRecord.Text), typeof(string))
                {
                    StorageName = DocumentChunkPayloadEncoding.TextStorageName,
                },
                new VectorStoreDataProperty(nameof(DocumentChunkRecord.PageNumber), typeof(int?))
                {
                    StorageName = DocumentChunkPayloadEncoding.PageNumberStorageName,
                },
                new VectorStoreVectorProperty(nameof(DocumentChunkRecord.Embedding), typeof(ReadOnlyMemory<float>), dimensions)
                {
                    DistanceFunction = DistanceFunction.CosineSimilarity,
                    IndexKind = IndexKind.Hnsw,
                },
            }
        };
    }
}
