using System.Threading.Tasks;
using Dignite.Paperbase.Abstractions.Documents;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace Dignite.Paperbase.Documents.Pipelines.FieldExtraction;

/// <summary>
/// 统一字段抽取 EventHandler（字段架构 v2）。订阅 <see cref="DocumentClassifiedEto"/>，分类完成后级联触发
/// 字段抽取。自 #289 步骤 1 起，核心抽取动作（读字段定义 → LLM → 守卫 → <c>SetFields</c> → 发
/// <see cref="FieldsExtractedEto"/>）下沉到可复用的 <see cref="FieldExtractionService"/>；本 handler 只做事件层
/// 适配：空 TypeCode 早退 + 把事件载荷翻译成引擎入参（含 stale reclassify 事件早退优化）。
/// <para>
/// 跨租户守卫、in-flight reclassify race 守卫、UoW 三段式短事务纪律均由引擎统一承载（CLAUDE.md "## 安全约定" +
/// <c>.claude/rules/background-jobs.md</c>）。handler 上保留 <c>[UnitOfWork(IsDisabled = true)]</c> 关掉 ambient UoW，
/// 让引擎内部各阶段自行 <c>requiresNew</c> 短 UoW、LLM 外部调用永不被长事务包住。
/// </para>
/// </summary>
public class FieldExtractionEventHandler
    : IDistributedEventHandler<DocumentClassifiedEto>, ITransientDependency
{
    private readonly FieldExtractionService _fieldExtractionService;

    public FieldExtractionEventHandler(FieldExtractionService fieldExtractionService)
    {
        _fieldExtractionService = fieldExtractionService;
    }

    [UnitOfWork(IsDisabled = true)]
    public virtual async Task HandleEventAsync(DocumentClassifiedEto eventData)
    {
        if (string.IsNullOrWhiteSpace(eventData.DocumentTypeCode))
        {
            return;
        }

        // 事件携带的 TypeCode 作为 stale reclassify 事件早退提示传入；引擎按 Document 当前 DocumentTypeId 抽取（#207）。
        await _fieldExtractionService.ExtractAsync(
            eventData.DocumentId,
            eventData.TenantId,
            expectedEventTypeCode: eventData.DocumentTypeCode);
    }
}
