using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents.Pipelines.Classification;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace Dignite.Paperbase.Documents.Pipelines.Reprocessing;

/// <summary>
/// 批量重新分类的分发任务（#289 步骤 5）——与字段重抽 dispatcher 同一链式自延续底座，区别在跑 <c>classification</c>
/// pipeline（成功必连带字段重抽）+ 范围条件（类型 / 待审核队列 / 保护人工确认）。每批 keyset 分页只读 Id →
/// enqueue 这批单篇 <see cref="DocumentClassificationBackgroundJob"/>（<c>PipelineRunId=null</c> → 作业内 StartAsync
/// 建新 classification run）→ 满批则 enqueue 下一个 dispatcher（带游标）→ 结束。
/// <para>
/// 破坏性（#289 场景一不对称点）：重新分类会覆盖自动分类、低置信度打回待审核并清字段。范围 / 保护人工确认
/// 已在 <see cref="DocumentReclassificationDispatcherArgs"/> 编码进 id 范围查询——dispatcher 只读 Id、不在此判 per-doc。
/// </para>
/// <para>
/// 生命周期：classification 是 key pipeline，重排会把已 Ready 文档暂时打回 Processing（与单篇「重新识别」#263 同行为），
/// 完成后据置信度重新派生（达标 → 回 Ready；不达标 → 待人工审核）。
/// </para>
/// </summary>
[BackgroundJobName("Paperbase.DocumentReclassificationDispatcher")]
public class DocumentReclassificationDispatcherJob
    : AsyncBackgroundJob<DocumentReclassificationDispatcherArgs>, ITransientDependency
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public DocumentReclassificationDispatcherJob(
        IDocumentRepository documentRepository,
        IBackgroundJobManager backgroundJobManager,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _documentRepository = documentRepository;
        _backgroundJobManager = backgroundJobManager;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public override async Task ExecuteAsync(DocumentReclassificationDispatcherArgs args)
    {
        var batchSize = DocumentConsts.ReprocessingDispatchBatchSize;

        using (_currentTenant.Change(args.TenantId))
        {
            List<Guid> ids;

            using (var uow = _unitOfWorkManager.Begin(requiresNew: true))
            {
                ids = await _documentRepository.GetIdsForReprocessingAsync(
                    documentTypeId: args.DocumentTypeId,
                    reviewStatus: args.ReviewStatus,
                    excludeManuallyConfirmed: args.ExcludeManuallyConfirmed,
                    afterId: args.AfterId,
                    maxCount: batchSize);

                foreach (var id in ids)
                {
                    // PipelineRunId=null → 作业内 BeginOrStartAsync 走 StartAsync 建新 classification attempt。
                    await _backgroundJobManager.EnqueueAsync(
                        new DocumentClassificationJobArgs { DocumentId = id });
                }

                if (ids.Count == batchSize)
                {
                    await _backgroundJobManager.EnqueueAsync(
                        new DocumentReclassificationDispatcherArgs
                        {
                            DocumentTypeId = args.DocumentTypeId,
                            ReviewStatus = args.ReviewStatus,
                            ExcludeManuallyConfirmed = args.ExcludeManuallyConfirmed,
                            TenantId = args.TenantId,
                            AfterId = ids[^1]
                        });
                }

                await uow.CompleteAsync();
            }

            Logger.LogInformation(
                "Reclassification dispatcher: enqueued {Count} document(s) (type={DocumentTypeId}, reviewStatus={ReviewStatus}, excludeConfirmed={ExcludeConfirmed}, afterId={AfterId}, continued={Continued}).",
                ids.Count, args.DocumentTypeId, args.ReviewStatus, args.ExcludeManuallyConfirmed, args.AfterId, ids.Count == batchSize);
        }
    }
}

public class DocumentReclassificationDispatcherArgs
{
    /// <summary>非空 = 仅该类型（<see cref="ReclassificationScope.OnlyCurrentType"/>）；空 = 全量 / 跨类型。</summary>
    public Guid? DocumentTypeId { get; set; }

    /// <summary>非空 = 仅该审核状态（待审核队列范围传 <see cref="DocumentReviewStatus.PendingReview"/>）。</summary>
    public DocumentReviewStatus? ReviewStatus { get; set; }

    /// <summary>true = 排除已人工确认（<see cref="DocumentReviewStatus.Reviewed"/>）的文档（保护人工确认）。</summary>
    public bool ExcludeManuallyConfirmed { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>keyset 游标：仅枚举 <c>Id &gt; AfterId</c> 的文档；首批为 null。</summary>
    public Guid? AfterId { get; set; }
}
