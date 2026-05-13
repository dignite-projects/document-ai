using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents;
using Dignite.Paperbase.Vectors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Dignite.Paperbase.Documents.Pipelines.Embedding;

[BackgroundJobName("Paperbase.DocumentEmbedding")]
public class DocumentEmbeddingBackgroundJob
    : AsyncBackgroundJob<DocumentEmbeddingJobArgs>, ITransientDependency
{
    private readonly IDocumentRepository _documentRepository;
    private readonly DocumentPipelineRunManager _pipelineRunManager;
    private readonly DocumentPipelineRunAccessor _pipelineRunAccessor;
    private readonly DocumentEmbeddingWorkflow _workflow;
    private readonly DocumentChunkCollectionProvider _collectionProvider;
    private readonly PaperbaseVectorStoreOptions _vectorStoreOptions;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public DocumentEmbeddingBackgroundJob(
        IDocumentRepository documentRepository,
        DocumentPipelineRunManager pipelineRunManager,
        DocumentPipelineRunAccessor pipelineRunAccessor,
        DocumentEmbeddingWorkflow workflow,
        DocumentChunkCollectionProvider collectionProvider,
        IOptions<PaperbaseVectorStoreOptions> vectorStoreOptions,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _documentRepository = documentRepository;
        _pipelineRunManager = pipelineRunManager;
        _pipelineRunAccessor = pipelineRunAccessor;
        _workflow = workflow;
        _collectionProvider = collectionProvider;
        _vectorStoreOptions = vectorStoreOptions.Value;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public override async Task ExecuteAsync(DocumentEmbeddingJobArgs args)
    {
        var workItem = await BeginRunAsync(args);
        if (workItem == null)
        {
            return;
        }

        try
        {
            var chunks = await _workflow.RunAsync(workItem.Markdown);

            // Vector-store writes happen here, outside any ambient UoW (BeginRunAsync's UoW
            // has already committed) — same rule as the old IDocumentKnowledgeIndex call.
            // TenantId carried explicitly via workItem so Hangfire executions without an
            // ambient ICurrentTenant still scope correctly.
            await UpsertDocumentChunksAsync(workItem, chunks);

            await CompleteRunAsync(workItem.DocumentId, workItem.RunId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Embedding failed for document {DocumentId}. Pipeline run marked failed, lifecycle unchanged.",
                workItem.DocumentId);
            await FailRunAsync(workItem.DocumentId, workItem.RunId, ex.Message);
        }
    }

    // Whole-document replace semantics ported from the old IDocumentKnowledgeIndex.UpsertDocumentAsync:
    //  1. derive deterministic keys for the current chunk set
    //  2. UpsertAsync — Qdrant overwrites points that share the same key
    //  3. GetAsync(filter on tenant+document) — find pre-existing points whose keys are no longer
    //     in the current set (stragglers from a previous run with more chunks). Client-side filter
    //     because MEVD's LINQ surface doesn't translate `!keysToKeep.Contains(r.Id)` to Qdrant.
    //  4. DeleteAsync(stragglerKeys), then re-page until no stragglers remain.
    // Page size must fit all keepers + headroom for stragglers in one scan; we widen it
    // dynamically so a doc with N chunks always sees the full set, with CleanupMaxIterations
    // as a safety cap against eventual-consistency loops.
    protected virtual async Task UpsertDocumentChunksAsync(
        EmbeddingWorkItem workItem,
        IReadOnlyList<DocumentEmbeddingChunk> chunks)
    {
        var collection = await _collectionProvider.GetAsync();
        var tenantKey = DocumentChunkPayloadEncoding.EncodeTenantId(workItem.TenantId);
        var docKey = DocumentChunkPayloadEncoding.EncodeDocumentId(workItem.DocumentId);

        var records = chunks.Select(c => new DocumentChunkRecord
        {
            Id = DocumentChunkPointId.Create(workItem.TenantId, workItem.DocumentId, c.ChunkIndex),
            TenantId = tenantKey,
            DocumentId = docKey,
            DocumentTypeCode = workItem.DocumentTypeCode,
            ChunkIndex = c.ChunkIndex,
            Text = c.ChunkText,
            Embedding = c.Vector
        }).ToList();

        var keysToKeep = records.Select(r => r.Id).ToHashSet();

        if (records.Count > 0)
        {
            await collection.UpsertAsync(records);
        }

        // Scan must fit the entire keeper set plus CleanupPageSize worth of stragglers
        // in a single page so we can distinguish "no stragglers left" from "page filled
        // with keepers, stragglers behind". When the doc is huge enough that even this
        // overflows, the loop pages through removed stragglers across iterations.
        var pageSize = keysToKeep.Count + _vectorStoreOptions.CleanupPageSize;

        for (var iteration = 0; iteration < _vectorStoreOptions.CleanupMaxIterations; iteration++)
        {
            var stragglers = new List<Guid>();
            var totalScanned = 0;
            await foreach (var existing in collection.GetAsync(
                r => r.TenantId == tenantKey && r.DocumentId == docKey,
                top: pageSize))
            {
                totalScanned++;
                if (!keysToKeep.Contains(existing.Id))
                {
                    stragglers.Add(existing.Id);
                }
            }

            if (stragglers.Count > 0)
            {
                await collection.DeleteAsync(stragglers);
            }

            // Converged: either no stragglers this page, or the page wasn't full
            // (we saw the whole remaining set).
            if (stragglers.Count == 0 || totalScanned < pageSize)
            {
                return;
            }
        }

        Logger.LogWarning(
            "Stale-chunk cleanup for document {DocumentId} hit the {Cap}-iteration safety cap; some chunks may still be present.",
            workItem.DocumentId,
            _vectorStoreOptions.CleanupMaxIterations);
    }

    private async Task<EmbeddingWorkItem?> BeginRunAsync(DocumentEmbeddingJobArgs args)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);

        var document = await _documentRepository.GetAsync(args.DocumentId, includeDetails: true);
        if (string.IsNullOrWhiteSpace(document.Markdown))
        {
            if (args.PipelineRunId.HasValue)
            {
                var pendingRun = document.GetRun(args.PipelineRunId.Value);
                if (pendingRun != null)
                {
                    await _pipelineRunManager.SkipAsync(document, pendingRun, "document markdown is empty");
                    await _documentRepository.UpdateAsync(document, autoSave: true);
                }
            }

            await uow.CompleteAsync();
            return null;
        }

        var run = await _pipelineRunAccessor.BeginOrStartAsync(
            document, args.PipelineRunId, PaperbasePipelines.Embedding);
        await _documentRepository.UpdateAsync(document, autoSave: true);

        await uow.CompleteAsync();

        return new EmbeddingWorkItem(
            run.Id,
            document.Id,
            document.TenantId,
            document.DocumentTypeCode,
            document.Markdown);
    }

    private async Task CompleteRunAsync(Guid documentId, Guid runId)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);

        var document = await _documentRepository.GetAsync(documentId, includeDetails: true);
        var run = document.GetRun(runId)
            ?? await _pipelineRunAccessor.BeginOrStartAsync(
                document, runId, PaperbasePipelines.Embedding);

        await _pipelineRunManager.CompleteAsync(document, run);
        await _documentRepository.UpdateAsync(document, autoSave: true);

        await uow.CompleteAsync();
    }

    private async Task FailRunAsync(Guid documentId, Guid runId, string errorMessage)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);

        var document = await _documentRepository.GetAsync(documentId, includeDetails: true);
        var run = document.GetRun(runId)
            ?? await _pipelineRunAccessor.BeginOrStartAsync(
                document, runId, PaperbasePipelines.Embedding);

        await _pipelineRunManager.FailAsync(document, run, errorMessage);
        await _documentRepository.UpdateAsync(document, autoSave: true);

        await uow.CompleteAsync();
    }

    protected internal sealed record EmbeddingWorkItem(
        Guid RunId,
        Guid DocumentId,
        Guid? TenantId,
        string? DocumentTypeCode,
        string Markdown);
}

public class DocumentEmbeddingJobArgs
{
    public Guid DocumentId { get; set; }
    public Guid? PipelineRunId { get; set; }
}
