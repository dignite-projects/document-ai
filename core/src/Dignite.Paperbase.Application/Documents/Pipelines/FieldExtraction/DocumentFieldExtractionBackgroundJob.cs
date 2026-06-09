using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Dignite.Paperbase.Documents.Pipelines.FieldExtraction;

/// <summary>
/// <c>field-extraction</c> 流水线后台作业（#289 步骤 2）——「按需 / 批量字段重抽」的执行单元。
/// 复用 <see cref="DocumentPipelineBackgroundJobBase{TArgs}"/> 拿三段式短 UoW + <see cref="DocumentPipelineRun"/>
/// 可观测 / 失败重试，外部 LLM 抽取委托 #289 步骤 1 的共享引擎 <see cref="FieldExtractionService"/>。
/// <para>
/// <b>生命周期中性</b>：<see cref="PaperbasePipelines.FieldExtraction"/> 不在 <see cref="PaperbasePipelines.KeyPipelines"/>，
/// 故 BeginRun / CompleteRun 触发的 <c>DeriveLifecycleAsync</c> 不改 <c>Document.LifecycleStatus</c>——已 Ready 文档
/// 重抽字段后仍 Ready，不被打回 Processing。
/// </para>
/// <para>
/// 与分类作业一致的三阶段：BeginRun（短 UoW 建 / 续 run 并标记 Running）→ 外部 LLM 抽取（无 UoW）→
/// CompleteRun（短 UoW 标记 Succeeded）。任一异常 → <see cref="DocumentPipelineBackgroundJobBase{TArgs}.FailRunAsync"/>
/// 标记 Failed 后 re-throw 触发 ABP 后台作业重试。
/// </para>
/// </summary>
[BackgroundJobName("Paperbase.DocumentFieldExtraction")]
public class DocumentFieldExtractionBackgroundJob
    : DocumentPipelineBackgroundJobBase<DocumentFieldExtractionJobArgs>, ITransientDependency
{
    private readonly FieldExtractionService _fieldExtractionService;

    public DocumentFieldExtractionBackgroundJob(
        IDocumentRepository documentRepository,
        IDocumentPipelineRunRepository runRepository,
        DocumentPipelineRunManager pipelineRunManager,
        DocumentPipelineRunAccessor pipelineRunAccessor,
        IUnitOfWorkManager unitOfWorkManager,
        FieldExtractionService fieldExtractionService)
        : base(documentRepository, runRepository, pipelineRunManager, pipelineRunAccessor, unitOfWorkManager)
    {
        _fieldExtractionService = fieldExtractionService;
    }

    public override async Task ExecuteAsync(DocumentFieldExtractionJobArgs args)
    {
        var (documentId, runId, tenantId) = await BeginRunAsync(args);

        try
        {
            // 外部 LLM 抽取——引擎内部自行 ICurrentTenant.Change(tenantId) + 三段式短 UoW，绝不在任何 UoW 内调用。
            await _fieldExtractionService.ExtractAsync(documentId, tenantId);
            await CompleteRunAsync(documentId, runId);
        }
        catch (Exception ex)
        {
            await FailRunAsync(documentId, runId, ex.Message, PaperbasePipelines.FieldExtraction);
            throw;
        }
    }

    private async Task<(Guid DocumentId, Guid RunId, Guid? TenantId)> BeginRunAsync(DocumentFieldExtractionJobArgs args)
    {
        using var uow = UnitOfWorkManager.Begin(requiresNew: true);

        var document = await DocumentRepository.GetAsync(args.DocumentId, includeDetails: false);
        var run = await PipelineRunAccessor.BeginOrStartAsync(
            document, args.PipelineRunId, PaperbasePipelines.FieldExtraction);
        await DocumentRepository.UpdateAsync(document, autoSave: true);

        await uow.CompleteAsync();

        return (document.Id, run.Id, document.TenantId);
    }

    private async Task CompleteRunAsync(Guid documentId, Guid runId)
    {
        using var uow = UnitOfWorkManager.Begin(requiresNew: true);

        var (document, run) = await LoadDocumentAndRunAsync(documentId, runId, PaperbasePipelines.FieldExtraction);
        await PipelineRunManager.CompleteAsync(document, run);
        await DocumentRepository.UpdateAsync(document, autoSave: true);

        await uow.CompleteAsync();
    }
}

public class DocumentFieldExtractionJobArgs
{
    public Guid DocumentId { get; set; }
    public Guid? PipelineRunId { get; set; }
}
