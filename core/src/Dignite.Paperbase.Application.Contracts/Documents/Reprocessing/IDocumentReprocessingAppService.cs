using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Paperbase.Documents.Reprocessing;

/// <summary>
/// 存量文档批量重处理（#289）——配置（分类提示词 / 字段定义）调整后对存量重跑。两类共用「人工触发 + 预览 +
/// 链式分发 + 单篇幂等」执行底座，区别在跑哪个 pipeline / 范围 / 级联深度 / 警告严重度：
/// <list type="bullet">
///   <item><b>字段重抽</b>（叶子、安全、轻警告）：固定按文档类型范围，只跑 <c>field-extraction</c>、不重排分类。</item>
///   <item><b>重新分类</b>（级联、破坏性、重警告）：范围由人选，跑 <c>classification</c>（成功必连带字段重抽），默认保护人工确认。</item>
/// </list>
/// <para>「判断归人」（#289）：系统不替人猜要不要重处理 / 范围多大——配置变更零级联，重处理由人工随时发起。</para>
/// </summary>
public interface IDocumentReprocessingAppService : IApplicationService
{
    /// <summary>字段重抽预览：受影响文档数 + 该类型当前字段清单。</summary>
    Task<FieldReextractionPreviewDto> PreviewFieldExtractionAsync(Guid documentTypeId);

    /// <summary>触发字段重抽（按类型范围 enqueue dispatcher，立即返回预估文档数）。</summary>
    Task<ReprocessingStartResultDto> StartFieldExtractionAsync(StartFieldReextractionInput input);

    /// <summary>重新分类预览：按范围 + 保护人工确认开关 count 受影响文档数。</summary>
    Task<ReclassificationPreviewDto> PreviewReclassificationAsync(ReclassificationScopeInput input);

    /// <summary>触发重新分类（按范围 enqueue dispatcher，立即返回预估文档数）。</summary>
    Task<ReprocessingStartResultDto> StartReclassificationAsync(ReclassificationScopeInput input);
}
