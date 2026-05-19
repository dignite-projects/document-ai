using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Paperbase.Abstractions.Documents;
using Dignite.Paperbase.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Dignite.Paperbase.Documents.Pipelines.Classification;

[BackgroundJobName("Paperbase.DocumentClassification")]
public class DocumentClassificationBackgroundJob
    : AsyncBackgroundJob<DocumentClassificationJobArgs>, ITransientDependency
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly DocumentPipelineRunManager _pipelineRunManager;
    private readonly DocumentPipelineRunAccessor _pipelineRunAccessor;
    private readonly DocumentClassificationWorkflow _workflow;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IClock _clock;
    private readonly PaperbaseAIBehaviorOptions _aiOptions;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public DocumentClassificationBackgroundJob(
        IDocumentRepository documentRepository,
        IDocumentTypeRepository documentTypeRepository,
        DocumentPipelineRunManager pipelineRunManager,
        DocumentPipelineRunAccessor pipelineRunAccessor,
        DocumentClassificationWorkflow workflow,
        IDistributedEventBus distributedEventBus,
        IClock clock,
        IOptions<PaperbaseAIBehaviorOptions> aiOptions,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _documentRepository = documentRepository;
        _documentTypeRepository = documentTypeRepository;
        _pipelineRunManager = pipelineRunManager;
        _pipelineRunAccessor = pipelineRunAccessor;
        _workflow = workflow;
        _distributedEventBus = distributedEventBus;
        _clock = clock;
        _aiOptions = aiOptions.Value;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public override async Task ExecuteAsync(DocumentClassificationJobArgs args)
    {
        var workItem = await BeginRunAsync(args);

        try
        {
            var outcome = await ClassifyAsync(workItem);
            await CompleteRunAsync(workItem, outcome);
        }
        catch (Exception ex)
        {
            await FailRunAsync(workItem.DocumentId, workItem.RunId, ex.Message);
            throw;
        }
    }

    private async Task<ClassificationWorkItem> BeginRunAsync(DocumentClassificationJobArgs args)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);

        var document = await _documentRepository.GetAsync(args.DocumentId, includeDetails: true);

        // 候选集组装：当前租户可见的类型（Host + 当前租户私有），按 Priority DESC + 截断
        // 字段架构 v2：从 IDocumentTypeRepository.GetVisibleAsync 读 DB，替代 v1 进程内 DocumentTypeOptions.Types
        List<DocumentType> candidates;
        using (_currentTenant.Change(document.TenantId))
        {
            var visible = await _documentTypeRepository.GetVisibleAsync(document.TenantId);
            candidates = visible
                .Take(_aiOptions.MaxDocumentTypesInClassificationPrompt)
                .ToList();
        }

        var run = await _pipelineRunAccessor.BeginOrStartAsync(
            document, args.PipelineRunId, PaperbasePipelines.Classification);
        await _documentRepository.UpdateAsync(document, autoSave: true);

        await uow.CompleteAsync();

        return new ClassificationWorkItem(run.Id, document.Id, document.TenantId, document.Markdown ?? string.Empty, candidates);
    }

    private async Task<DocumentClassificationOutcome> ClassifyAsync(ClassificationWorkItem workItem)
    {
        try
        {
            return await _workflow.RunAsync(workItem.Candidates, workItem.Markdown);
        }
        catch (Exception ex) when (IsSchemaDeserializationError(ex))
        {
            Logger.LogWarning(ex,
                "AI classification response failed JSON deserialization for document {DocumentId}; routing to PendingReview.",
                workItem.DocumentId);
            return new DocumentClassificationOutcome
            {
                TypeCode = null,
                ConfidenceScore = 0,
                Reason = "AI response could not be parsed (schema drift)."
            };
        }
    }

    private async Task CompleteRunAsync(
        ClassificationWorkItem workItem,
        DocumentClassificationOutcome outcome)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);

        var document = await _documentRepository.GetAsync(workItem.DocumentId, includeDetails: true);
        var run = document.GetRun(workItem.RunId)
            ?? await _pipelineRunAccessor.BeginOrStartAsync(
                document, workItem.RunId, PaperbasePipelines.Classification);

        await ApplyClassificationResultAsync(document, run, outcome);
        await _documentRepository.UpdateAsync(document, autoSave: true);

        await uow.CompleteAsync();
    }

    private async Task FailRunAsync(Guid documentId, Guid runId, string errorMessage)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);

        var document = await _documentRepository.GetAsync(documentId, includeDetails: true);
        var run = document.GetRun(runId)
            ?? await _pipelineRunAccessor.BeginOrStartAsync(
                document, runId, PaperbasePipelines.Classification);

        await _pipelineRunManager.FailAsync(document, run, errorMessage);
        await _documentRepository.UpdateAsync(document, autoSave: true);

        await uow.CompleteAsync();
    }

    private static bool IsSchemaDeserializationError(Exception ex)
        => ex is JsonException || ex.GetBaseException() is JsonException;

    private async Task ApplyClassificationResultAsync(
        Document document,
        DocumentPipelineRun run,
        DocumentClassificationOutcome outcome)
    {
        // 字段架构 v2：从 DB 查 type definition（替代 v1 DocumentTypeOptions.Types lookup）
        DocumentType? typeDef = null;
        if (!string.IsNullOrEmpty(outcome.TypeCode))
        {
            using (_currentTenant.Change(document.TenantId))
            {
                typeDef = await _documentTypeRepository.FindByTypeCodeAsync(document.TenantId, outcome.TypeCode);
            }
        }

        if (typeDef != null && outcome.ConfidenceScore >= typeDef.ConfidenceThreshold)
        {
            await _pipelineRunManager.CompleteClassificationAsync(
                document, run, typeDef.TypeCode, outcome.ConfidenceScore);

            await _distributedEventBus.PublishAsync(
                new DocumentClassifiedEto
                {
                    DocumentId = document.Id,
                    TenantId = document.TenantId,
                    EventTime = _clock.Now,
                    DocumentTypeCode = typeDef.TypeCode,
                    ClassificationConfidence = outcome.ConfidenceScore,
                    Markdown = document.Markdown
                });

            return;
        }

        var candidates = outcome.Candidates
            .Select(c => new PipelineRunCandidate(c.TypeCode, c.ConfidenceScore))
            .ToList();

        await _pipelineRunManager.CompleteClassificationWithLowConfidenceAsync(
            document, run, outcome.Reason, candidates);
    }

    private sealed record ClassificationWorkItem(
        Guid RunId,
        Guid DocumentId,
        Guid? TenantId,
        string Markdown,
        IReadOnlyList<DocumentType> Candidates);
}

public class DocumentClassificationJobArgs
{
    public Guid DocumentId { get; set; }
    public Guid? PipelineRunId { get; set; }
}
