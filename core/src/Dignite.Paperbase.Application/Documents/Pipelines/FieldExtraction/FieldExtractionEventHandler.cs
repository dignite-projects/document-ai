using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
/// TenantId IS NULL 字段；租户文档用对应租户字段），跑 LLM 抽取，写入
/// <c>Document.ExtractedFields</c>（单一 Dictionary，源由 Document.TenantId 决定，
/// 不分桶不存在跨层命名冲突）。统一发布 <see cref="FieldsExtractedEto"/>。
/// <para>
/// 安全约束（CLAUDE.md）：显式恢复事件携带的 TenantId 上下文；显式 TenantId 谓词查询；
/// 跨租户断言（防 ambient filter 被 disable）。
/// </para>
/// </summary>
public class FieldExtractionEventHandler
    : IDistributedEventHandler<DocumentClassifiedEto>, ITransientDependency
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFieldDefinitionRepository _fieldDefinitionRepository;
    private readonly FieldExtractionWorkflow _workflow;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IClock _clock;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<FieldExtractionEventHandler> _logger;

    public FieldExtractionEventHandler(
        IDocumentRepository documentRepository,
        IFieldDefinitionRepository fieldDefinitionRepository,
        FieldExtractionWorkflow workflow,
        IDistributedEventBus distributedEventBus,
        IClock clock,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<FieldExtractionEventHandler> logger)
    {
        _documentRepository = documentRepository;
        _fieldDefinitionRepository = fieldDefinitionRepository;
        _workflow = workflow;
        _distributedEventBus = distributedEventBus;
        _clock = clock;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public async Task HandleEventAsync(DocumentClassifiedEto eventData)
    {
        if (string.IsNullOrWhiteSpace(eventData.DocumentTypeCode))
        {
            return;
        }

        // 显式恢复事件携带的租户上下文 —— 分布式事件 handler 在 IIS / Hangfire worker
        // 上下文中 ICurrentTenant 不一定自动还原。
        using (_currentTenant.Change(eventData.TenantId))
        {
            // 解读 X：按 Document 所属租户精确查单层字段定义
            var definitions = await _fieldDefinitionRepository.GetForExtractionAsync(
                eventData.TenantId, eventData.DocumentTypeCode);

            if (definitions.Count == 0)
            {
                // 该 (TenantId, DocumentTypeCode) 下无字段定义——直接发空事件让下游 DocumentReady 推进。
                // 单一事件场景无需 UoW 包裹（ABP outbox 在调用方 UoW 内会自动入队；
                // 这里无 DB 写入，进 outbox 不是必需，at-least-once 直发即可）。
                await PublishFieldsExtractedAsync(eventData, fieldCount: 0);
                return;
            }

            var descriptors = definitions.Select(d => new FieldExtractionDescriptor(
                d.Name, d.Prompt, d.DataType, d.IsRequired)).ToList();

            var extracted = await _workflow.ExtractAsync(descriptors, eventData.Markdown ?? string.Empty);

            using var uow = _unitOfWorkManager.Begin(requiresNew: true);

            var document = await _documentRepository.FindAsync(eventData.DocumentId, includeDetails: false);
            if (document == null)
            {
                _logger.LogWarning(
                    "DocumentClassifiedEto for missing document {DocumentId} — field extraction skipped.",
                    eventData.DocumentId);
                return;
            }

            // 跨租户断言（防 ambient DataFilter 被 disable 的路径）
            if (document.TenantId != eventData.TenantId)
            {
                _logger.LogWarning(
                    "Cross-tenant DocumentClassifiedEto received: event tenant={EventTenant} document tenant={DocTenant} document={DocId}",
                    eventData.TenantId, document.TenantId, eventData.DocumentId);
                return;
            }

            // 非空字段写入 ExtractedFields（单层，无分桶）
            var fields = new Dictionary<string, JsonElement>();
            foreach (var d in descriptors)
            {
                if (extracted.TryGetValue(d.Name, out var value) && value.HasValue)
                {
                    fields[d.Name] = value.Value;
                }
            }

            document.SetExtractedFields(fields.Count > 0 ? fields : null);

            await _documentRepository.UpdateAsync(document, autoSave: true);

            // 在 UoW 内 publish，让 ABP transactional outbox 把事件与 Document.ExtractedFields
            // 的写入原子地一起持久化——避免"字段写入成功但事件丢失"。
            await PublishFieldsExtractedAsync(eventData, fields.Count);

            await uow.CompleteAsync();

            _logger.LogInformation(
                "Field extraction for document {DocumentId} produced {NonNullCount}/{TotalCount} non-null values.",
                eventData.DocumentId, fields.Count, definitions.Count);
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
