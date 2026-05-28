using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Paperbase.Abstractions.Documents;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Dignite.Paperbase.Documents.Pipelines.FieldExtraction;

/// <summary>
/// 统一字段抽取 EventHandler（字段架构 v2 + 解读 X）。订阅 <see cref="DocumentClassifiedEto"/>：
/// 分类完成后按 Document 所属租户精确查 <see cref="FieldDefinition"/> 一层（Host 文档用
/// TenantId IS NULL 字段；租户文档用对应租户字段），跑 LLM 抽取，经 <c>Document.SetFields</c> 整组写入
/// <see cref="Document.ExtractedFieldValues"/>（typed child 集合，源由 Document.TenantId 决定，
/// 不分桶不存在跨层命名冲突）。统一发布 <see cref="FieldsExtractedEto"/>。
/// <para>
/// 安全约束（CLAUDE.md "## 安全约定"）：显式 <see cref="ICurrentTenant.Change"/> 恢复事件携带的
/// TenantId 上下文，让 ABP <c>IMultiTenant</c> filter 自动按目标层隔离仓储查询；跨租户断言
/// （防 ambient filter 被 disable）；reclassify race 断言（防 stale 事件用旧 schema 污染）。
/// </para>
/// <para>
/// UoW 三段式（<c>.claude/rules/background-jobs.md</c>）：handler 上 <c>[UnitOfWork(IsDisabled = true)]</c>
/// 关掉 ambient UoW；读 FieldDefinition / 回查 Document.Markdown / LLM 调用 / 写 Document + publish 各阶段 begin
/// <c>requiresNew</c> 短 UoW——LLM 外部调用永远不被任何长事务包住，避免在高并发下 DB 连接 /
/// 锁 / transaction 跨整个 LLM 调用窗口而触发 SQL command timeout。
/// </para>
/// </summary>
public class FieldExtractionEventHandler
    : IDistributedEventHandler<DocumentClassifiedEto>, ITransientDependency
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly IFieldDefinitionRepository _fieldDefinitionRepository;
    private readonly FieldExtractionWorkflow _workflow;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<FieldExtractionEventHandler> _logger;

    public FieldExtractionEventHandler(
        IDocumentRepository documentRepository,
        IDocumentTypeRepository documentTypeRepository,
        IFieldDefinitionRepository fieldDefinitionRepository,
        FieldExtractionWorkflow workflow,
        IDistributedEventBus distributedEventBus,
        IClock clock,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<FieldExtractionEventHandler> logger)
    {
        _documentRepository = documentRepository;
        _documentTypeRepository = documentTypeRepository;
        _fieldDefinitionRepository = fieldDefinitionRepository;
        _workflow = workflow;
        _distributedEventBus = distributedEventBus;
        _clock = clock;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    [UnitOfWork(IsDisabled = true)]
    public virtual async Task HandleEventAsync(DocumentClassifiedEto eventData)
    {
        if (string.IsNullOrWhiteSpace(eventData.DocumentTypeCode))
        {
            return;
        }

        // 显式恢复事件携带的租户上下文 —— 分布式事件 handler 在 IIS / Hangfire worker
        // 上下文中 ICurrentTenant 不一定自动还原。
        using (_currentTenant.Change(eventData.TenantId))
        {
            // 阶段 1：短 UoW —— 解析事件携带的 TypeCode → 内部 DocumentTypeId（#207），并按该 Id 读字段定义（单层）。
            // 显式 dispose 让该 UoW 完全退出，再进入阶段 2 的外部 LLM 调用。
            Guid documentTypeId;
            List<FieldDefinition> definitions;
            using (var readUow = _unitOfWorkManager.Begin(requiresNew: true))
            {
                var typeDef = await _documentTypeRepository.FindByTypeCodeAsync(eventData.DocumentTypeCode);
                if (typeDef == null)
                {
                    // 事件携带的类型码在当前层解析不到（类型在飞行期间被重命名 / 删除，罕见）——跳过，等新分类事件触发。
                    _logger.LogWarning(
                        "DocumentClassifiedEto type code {TypeCode} not resolvable in tenant {TenantId} " +
                        "(renamed/deleted in-flight?); field extraction skipped for doc {DocumentId}.",
                        eventData.DocumentTypeCode, eventData.TenantId, eventData.DocumentId);
                    await readUow.CompleteAsync();
                    return;
                }

                documentTypeId = typeDef.Id;
                definitions = await _fieldDefinitionRepository.GetForExtractionAsync(documentTypeId);
                await readUow.CompleteAsync();
            }

            // 空字段路径：目标类型无字段定义。仍需把该文档可能残留的旧 schema 字段行清空——
            // reclassify 从「有字段类型」换到「无字段类型」时，旧字段行不清会以新 TypeCode 被结构化检索 /
            // DTO 误带（违反「reclassify 整组替换、不残留旧 schema」语义）。短 UoW 内清空 + publish，
            // 由 ABP transactional outbox 原子接住事件（避免裸 publish 走非事务路径丢事件）。
            if (definitions.Count == 0)
            {
                using var clearUow = _unitOfWorkManager.Begin(requiresNew: true);

                var blankDocument = await _documentRepository.FindAsync(eventData.DocumentId, includeDetails: true);
                // 仅当文档仍存在、同租户、且当前类型仍是本事件的类型（非 reclassify race 的 stale 事件）才清空，
                // 避免用 stale 事件误删后续分类写入的字段。按内部 DocumentTypeId 比较（#207）。
                if (blankDocument != null
                    && blankDocument.TenantId == eventData.TenantId
                    && blankDocument.DocumentTypeId == documentTypeId
                    && blankDocument.ExtractedFieldValues.Count > 0)
                {
                    blankDocument.SetFields(Array.Empty<DocumentFieldValue>());
                    await _documentRepository.UpdateAsync(blankDocument, autoSave: true);
                }

                await PublishFieldsExtractedAsync(eventData, fieldCount: 0);
                await clearUow.CompleteAsync();
                return;
            }

            var descriptors = definitions.Select(d => new FieldExtractionDescriptor(
                d.Id, d.Name, d.Prompt, d.DataType, d.IsRequired)).ToList();

            string markdown;
            using (var documentReadUow = _unitOfWorkManager.Begin(requiresNew: true))
            {
                var readDocument = await _documentRepository.FindAsync(eventData.DocumentId, includeDetails: false);
                if (readDocument == null)
                {
                    _logger.LogWarning(
                        "DocumentClassifiedEto for missing document {DocumentId} — field extraction skipped.",
                        eventData.DocumentId);
                    return;
                }

                // 跨租户断言（防 ambient DataFilter 被 disable 的路径）。
                if (readDocument.TenantId != eventData.TenantId)
                {
                    _logger.LogWarning(
                        "Cross-tenant DocumentClassifiedEto received: event tenant={EventTenant} document tenant={DocTenant} document={DocId}",
                        eventData.TenantId, readDocument.TenantId, eventData.DocumentId);
                    return;
                }

                // 在 LLM 调用前先做一次 stale 事件防护，避免对已经重分类的文档做无意义外部调用。按内部 DocumentTypeId 比较（#207）。
                if (readDocument.DocumentTypeId != documentTypeId)
                {
                    _logger.LogInformation(
                        "Stale DocumentClassifiedEto before field extraction: event typeCode={EventTypeCode} (typeId={EventTypeId}) " +
                        "document typeId={DocTypeId} doc={DocumentId}.",
                        eventData.DocumentTypeCode, documentTypeId, readDocument.DocumentTypeId, eventData.DocumentId);
                    return;
                }

                markdown = readDocument.Markdown ?? string.Empty;
                await documentReadUow.CompleteAsync();
            }

            // 阶段 2：外部 LLM 调用，**不在任何 UoW 内**（background-jobs.md 硬约束）。
            // handler 上 [UnitOfWork(IsDisabled = true)] 关掉了 ambient UoW；
            // 字段定义读取和 Document.Markdown 回查的短 UoW 均已 dispose；
            // 到这里 _unitOfWorkManager.Current 应为 null。
            // 一旦不为 null 说明上述假设被未来改动打穿，立即 log 警告以暴露隐患。
            if (_unitOfWorkManager.Current != null)
            {
                _logger.LogWarning(
                    "FieldExtractionEventHandler entered external LLM call with ambient UoW present (doc={DocumentId}). " +
                    "This violates background-jobs.md (external work must not run inside a long-lived UoW). " +
                    "Check [UnitOfWork(IsDisabled = true)] on HandleEventAsync and readUow dispose ordering.",
                    eventData.DocumentId);
            }

            var extracted = await _workflow.ExtractAsync(descriptors, markdown);

            // 阶段 3：短 UoW 写 Document + publish FieldsExtractedEto——
            // 两件事在同一 UoW 内由 ABP outbox 原子持久化，避免"字段写入成功但事件丢失"。
            using var writeUow = _unitOfWorkManager.Begin(requiresNew: true);

            // includeDetails: true 让 ExtractedFieldValues child 集合一并加载——SetFields 走 reconcile
            // 需要现有字段行在场才能正确 diff（删旧 / 原地改同名 / 增新），避免 stale collection 漏删旧行。
            var document = await _documentRepository.FindAsync(eventData.DocumentId, includeDetails: true);
            if (document == null)
            {
                _logger.LogWarning(
                    "DocumentClassifiedEto for missing document {DocumentId} — field extraction skipped.",
                    eventData.DocumentId);
                return;
            }

            // 跨租户断言（防 ambient DataFilter 被 disable 的路径）。
            if (document.TenantId != eventData.TenantId)
            {
                _logger.LogWarning(
                    "Cross-tenant DocumentClassifiedEto received: event tenant={EventTenant} document tenant={DocTenant} document={DocId}",
                    eventData.TenantId, document.TenantId, eventData.DocumentId);
                return;
            }

            // Reclassify race 断言（at-least-once 投递 + 单调时间戳幂等的本地化实现）：
            // 若 Document 当前的 DocumentTypeCode 已与事件携带的 TypeCode 不一致，说明事件
            // 在飞行期间操作员 reclassify 过，本事件已 stale。继续抽取会用旧 schema 污染
            // ExtractedFields（出现"TypeCode=invoice 但 ExtractedFields 来自 contract schema"
            // 的脏状态）。安全做法：丢弃本事件，等新分类事件触发新一轮抽取。
            if (document.DocumentTypeId != documentTypeId)
            {
                _logger.LogInformation(
                    "Stale DocumentClassifiedEto: event typeCode={EventTypeCode} (typeId={EventTypeId}) document typeId={DocTypeId} doc={DocumentId}. " +
                    "Likely reclassified between publish and consume; discarding to avoid writing fields against an outdated schema.",
                    eventData.DocumentTypeCode, documentTypeId, document.DocumentTypeId, eventData.DocumentId);
                return;
            }

            // 非空字段构造 typed DocumentFieldValue（FieldDefinitionId + DataType 来自 descriptor，源自 FieldDefinition）——
            // 整组替换字段值集合（单层，无分桶；reconcile 删旧 / 改同字段 / 增新）。LLM 输出按 Name（prompt schema key）回取。
            var fieldValues = new List<DocumentFieldValue>();
            foreach (var d in descriptors)
            {
                if (extracted.TryGetValue(d.Name, out var value) && value.HasValue)
                {
                    fieldValues.Add(new DocumentFieldValue(d.FieldDefinitionId, d.DataType, value.Value));
                }
            }

            document.SetFields(fieldValues);

            await _documentRepository.UpdateAsync(document, autoSave: true);

            // 在 UoW 内 publish，让 ABP transactional outbox 把事件与字段值的写入原子地一起持久化——
            // 避免"字段写入成功但事件丢失"。
            await PublishFieldsExtractedAsync(eventData, fieldValues.Count);

            await writeUow.CompleteAsync();

            _logger.LogInformation(
                "Field extraction for document {DocumentId} produced {NonNullCount}/{TotalCount} non-null values.",
                eventData.DocumentId, fieldValues.Count, definitions.Count);
        }
    }

    private async Task PublishFieldsExtractedAsync(DocumentClassifiedEto source, int fieldCount)
    {
        await _distributedEventBus.PublishAsync(
            new FieldsExtractedEto
            {
                DocumentId = source.DocumentId,
                TenantId = source.TenantId,
                EventTime = _clock.Now,
                DocumentTypeCode = source.DocumentTypeCode,
                FieldCount = fieldCount
            });
    }
}
