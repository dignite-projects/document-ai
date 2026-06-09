using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents.DocumentTypes;
using Dignite.Paperbase.Documents.Fields;
using Dignite.Paperbase.Documents.Pipelines.Reprocessing;
using Dignite.Paperbase.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Entities;

namespace Dignite.Paperbase.Documents.Reprocessing;

/// <summary>
/// 存量文档批量重处理（#289）。人工触发 + 预览 + 链式分发 + 单篇幂等执行底座的应用层入口。
/// 触发只 enqueue 一个 dispatcher（点按钮立即返回），dispatcher 在后台 keyset 分页枚举范围、分批入队单篇任务。
/// <para>
/// 安全：admin 级 <see cref="PaperbasePermissions.Documents.Reprocessing"/> 权限；范围 count / 枚举均经 ABP
/// <c>IMultiTenant</c> 全局过滤器按 <see cref="ApplicationService.CurrentTenant"/> 自动隔离（不手写 TenantId 谓词，
/// dispatcher 据传入的 <c>CurrentTenant.Id</c> 还原 ambient 层）。
/// </para>
/// </summary>
public class DocumentReprocessingAppService : PaperbaseAppService, IDocumentReprocessingAppService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly IFieldDefinitionRepository _fieldDefinitionRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public DocumentReprocessingAppService(
        IDocumentRepository documentRepository,
        IDocumentTypeRepository documentTypeRepository,
        IFieldDefinitionRepository fieldDefinitionRepository,
        IBackgroundJobManager backgroundJobManager)
    {
        _documentRepository = documentRepository;
        _documentTypeRepository = documentTypeRepository;
        _fieldDefinitionRepository = fieldDefinitionRepository;
        _backgroundJobManager = backgroundJobManager;
    }

    [Authorize(PaperbasePermissions.Documents.Reprocessing.FieldExtraction)]
    public virtual async Task<FieldReextractionPreviewDto> PreviewFieldExtractionAsync(Guid documentTypeId)
    {
        await EnsureTypeInCurrentLayerAsync(documentTypeId);

        var count = await _documentRepository.CountForReprocessingAsync(
            documentTypeId, withReason: null, excludeManuallyConfirmed: false);
        var definitions = await _fieldDefinitionRepository.GetListAsync(documentTypeId);

        return new FieldReextractionPreviewDto
        {
            DocumentTypeId = documentTypeId,
            DocumentCount = count,
            FieldNames = definitions.Select(d => d.Name).ToList()
        };
    }

    [Authorize(PaperbasePermissions.Documents.Reprocessing.FieldExtraction)]
    public virtual async Task<ReprocessingStartResultDto> StartFieldExtractionAsync(StartFieldReextractionInput input)
    {
        await EnsureTypeInCurrentLayerAsync(input.DocumentTypeId);

        var count = await _documentRepository.CountForReprocessingAsync(
            input.DocumentTypeId, withReason: null, excludeManuallyConfirmed: false);

        Logger.LogInformation(
            "StartFieldExtraction user={UserId} tenant={TenantId} type={DocumentTypeId} estimatedCount={Count}",
            CurrentUser.Id, CurrentTenant.Id, input.DocumentTypeId, count);

        await _backgroundJobManager.EnqueueAsync(
            new DocumentFieldReextractionDispatcherArgs
            {
                DocumentTypeId = input.DocumentTypeId,
                TenantId = CurrentTenant.Id,
                AfterId = null
            });

        return new ReprocessingStartResultDto { EstimatedDocumentCount = count };
    }

    [Authorize(PaperbasePermissions.Documents.Reprocessing.Reclassification)]
    public virtual async Task<ReclassificationPreviewDto> PreviewReclassificationAsync(ReclassificationScopeInput input)
    {
        var (typeId, withReason, excludeConfirmed) = await ResolveScopeAsync(input);

        var count = await _documentRepository.CountForReprocessingAsync(typeId, withReason, excludeConfirmed);

        return new ReclassificationPreviewDto { DocumentCount = count };
    }

    [Authorize(PaperbasePermissions.Documents.Reprocessing.Reclassification)]
    public virtual async Task<ReprocessingStartResultDto> StartReclassificationAsync(ReclassificationScopeInput input)
    {
        var (typeId, withReason, excludeConfirmed) = await ResolveScopeAsync(input);

        var count = await _documentRepository.CountForReprocessingAsync(typeId, withReason, excludeConfirmed);

        Logger.LogInformation(
            "StartReclassification user={UserId} tenant={TenantId} scope={Scope} type={DocumentTypeId} withReason={WithReason} excludeConfirmed={ExcludeConfirmed} estimatedCount={Count}",
            CurrentUser.Id, CurrentTenant.Id, input.Scope, typeId, withReason, excludeConfirmed, count);

        await _backgroundJobManager.EnqueueAsync(
            new DocumentReclassificationDispatcherArgs
            {
                DocumentTypeId = typeId,
                WithReason = withReason,
                ExcludeManuallyConfirmed = excludeConfirmed,
                TenantId = CurrentTenant.Id,
                AfterId = null
            });

        return new ReprocessingStartResultDto { EstimatedDocumentCount = count };
    }

    /// <summary>把范围 DTO 翻译成仓储范围查询三元组，并校验 OnlyCurrentType 的类型存在于当前层。</summary>
    protected virtual async Task<(Guid? TypeId, DocumentReviewReasons? WithReason, bool ExcludeConfirmed)> ResolveScopeAsync(
        ReclassificationScopeInput input)
    {
        switch (input.Scope)
        {
            case ReclassificationScope.OnlyCurrentType:
                // DocumentTypeId 必填由 DTO IValidatableObject 保证；此处校验其存在于当前层。
                await EnsureTypeInCurrentLayerAsync(input.DocumentTypeId!.Value);
                return (input.DocumentTypeId, null, !input.IncludeManuallyConfirmed);

            case ReclassificationScope.AllDocuments:
                return (null, null, !input.IncludeManuallyConfirmed);

            case ReclassificationScope.PendingReviewQueue:
                // 待审核队列 = 分类未定（#284 两轴：UnresolvedClassification 原因，取代旧 PendingReview）。
                // 这些文档本就无已确认类型，IncludeManuallyConfirmed 无意义。
                return (null, DocumentReviewReasons.UnresolvedClassification, false);

            default:
                throw new ArgumentOutOfRangeException(nameof(input), input.Scope, "Unknown reclassification scope.");
        }
    }

    /// <summary>校验文档类型存在于当前 ambient 层（跨层 / 不存在 → <see cref="EntityNotFoundException"/>）。</summary>
    protected virtual async Task EnsureTypeInCurrentLayerAsync(Guid documentTypeId)
    {
        // FindAsync 经 ambient IMultiTenant 过滤器隔离——跨层 id 返回 null。
        _ = await _documentTypeRepository.FindAsync(documentTypeId)
            ?? throw new EntityNotFoundException(typeof(DocumentType), documentTypeId);
    }
}
