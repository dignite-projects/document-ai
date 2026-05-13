using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace Dignite.Paperbase.Vectors;

// Lives under Vectors/ — colocated with the cleanup feature it drives, matching the
// pattern set by Documents/Pipelines/RelationDiscovery/RelationDiscoveryEventHandler
// (handlers live with the feature they trigger, not next to the event source).
public class DocumentDeletingEventHandler :
    ILocalEventHandler<DocumentDeletingEvent>,
    ITransientDependency
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly DocumentChunkCollectionProvider _collectionProvider;
    private readonly PaperbaseVectorStoreOptions _options;
    private readonly ILogger<DocumentDeletingEventHandler> _logger;

    public DocumentDeletingEventHandler(
        IUnitOfWorkManager unitOfWorkManager,
        DocumentChunkCollectionProvider collectionProvider,
        IOptions<PaperbaseVectorStoreOptions> options,
        ILogger<DocumentDeletingEventHandler> logger)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _collectionProvider = collectionProvider;
        _options = options.Value;
        _logger = logger;
    }

    public virtual Task HandleEventAsync(DocumentDeletingEvent eventData)
    {
        var currentUnitOfWork = _unitOfWorkManager.Current;
        if (currentUnitOfWork == null)
        {
            _logger.LogWarning(
                "Document {DocumentId} delete event was handled without an active unit of work; vector cleanup was skipped.",
                eventData.DocumentId);
            return Task.CompletedTask;
        }

        var documentId = eventData.DocumentId;
        var tenantId = eventData.TenantId;

        currentUnitOfWork.OnCompleted(async () =>
        {
            try
            {
                await DeleteChunksAsync(tenantId, documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to delete document {DocumentId} chunks from the vector store after the document transaction committed.",
                    documentId);
            }
        });

        return Task.CompletedTask;
    }

    // MEVD has no filter-based bulk delete on the abstraction surface — list keys
    // via GetAsync(filter), DeleteAsync(keys), then re-query until the page returns
    // empty. Each iteration removes the records it scanned, so subsequent pages see
    // the remainder. The only exit condition is "GetAsync returned no records": MEVD
    // does not guarantee a single page returns the full `top` slice when more
    // records exist, so we don't trust `keys.Count < top` as a convergence signal —
    // one extra empty round-trip per cleanup is a cheap price for portability across
    // future connectors that may chunk results differently. CleanupMaxIterations
    // bounds runaway loops in case of eventual consistency edge cases.
    protected virtual async Task DeleteChunksAsync(Guid? tenantId, Guid documentId)
    {
        var collection = await _collectionProvider.GetAsync();
        var tenantKey = DocumentChunkPayloadEncoding.EncodeTenantId(tenantId);
        var docKey = DocumentChunkPayloadEncoding.EncodeDocumentId(documentId);

        for (var iteration = 0; iteration < _options.CleanupMaxIterations; iteration++)
        {
            var keys = new List<Guid>();
            await foreach (var record in collection.GetAsync(
                r => r.TenantId == tenantKey && r.DocumentId == docKey,
                top: _options.CleanupPageSize))
            {
                keys.Add(record.Id);
            }

            if (keys.Count == 0)
            {
                return;
            }

            await collection.DeleteAsync(keys);
        }

        _logger.LogWarning(
            "Vector cleanup for document {DocumentId} hit the {Cap}-iteration safety cap; some chunks may still be present.",
            documentId,
            _options.CleanupMaxIterations);
    }
}
