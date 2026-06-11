using System.Collections.Generic;

namespace Dignite.DocumentAI.Documents.Pipelines;

/// <summary>
/// 核心层定义的流水线标识常量。
/// 业务模块可注册自定义 PipelineCode（建议命名前缀 "{moduleCode}."），
/// 但不会被计入生命周期派生。
/// <para>
/// <see cref="TextExtraction"/> / <see cref="Classification"/> 必须是 <c>const</c>：
/// 它们持久化到 <c>DocumentPipelineRun.PipelineCode</c> 列、跨 JobArgs / ETO 载荷传递，
/// 且用作 <c>DocumentPipelineJobScheduler</c> switch expression 的 constant pattern。
/// 任何运行时 mutate 都会让历史 DB 数据按旧 code 写、新代码按新 code 查，分发逻辑断裂。
/// </para>
/// </summary>
public static class DocumentAIPipelines
{
    /// <summary>文本提取（OCR 或原生提取）。关键流水线。</summary>
    public const string TextExtraction = "text-extraction";

    /// <summary>文档分类（规则匹配 / AI）。关键流水线。</summary>
    public const string Classification = "classification";

    /// <summary>
    /// 类型绑定字段抽取（#289）。**非关键流水线、生命周期中性**——刻意不入 <see cref="KeyPipelines"/>，
    /// 故 <c>DocumentPipelineRunManager.DeriveLifecycleAsync</c> 不据它派生 <c>LifecycleStatus</c>，
    /// 重抽字段不会把已 Ready 文档打回 Processing。复用 <c>DocumentPipelineRun</c> 仅为拿可观测 + 重试，
    /// 不参与 Ready 闸门。字段抽取的级联仍由分类完成事件（<c>FieldExtractionEventHandler</c>）驱动；
    /// 本 pipeline 是「按需 / 批量字段重抽」重处理的独立触发入口。
    /// </summary>
    public const string FieldExtraction = "field-extraction";

    /// <summary>生命周期派生时视为"关键"的流水线集合。<see cref="FieldExtraction"/> 刻意不在其中（生命周期中性）。</summary>
    public static readonly IReadOnlyCollection<string> KeyPipelines = new[]
    {
        TextExtraction,
        Classification
    };

    /// <summary>
    /// 用户可手动重试的流水线集合。
    /// 业务模块自定义的流水线不通过此 API 暴露重试。
    /// </summary>
    public static readonly IReadOnlyCollection<string> RetryablePipelines = new[]
    {
        TextExtraction,
        Classification,
        FieldExtraction
    };
}
