using System;
using Volo.Abp.EventBus;

namespace Dignite.Paperbase.Abstractions.Documents;

/// <summary>
/// 全流水线完成 + 通过 OCR 置信度门槛后发布——下游消费方的"可信信号"。
/// 这是受置信度门槛约束的**唯一**生命周期事件：
/// <list type="bullet">
///   <item>OCR confidence ≥ 门槛 → 自动发布</item>
///   <item>OCR confidence &lt; 门槛 → 文档进待人工审核队列；操作员通过后才发布</item>
/// </list>
/// 大多数下游业务消费方应订阅此事件而非早期阶段事件（DocumentUploaded/OCRCompleted/...）。
/// </summary>
[EventName("Paperbase.Document.Ready")]
public class DocumentReadyEto
{
    public string Version { get; set; } = "1.0";

    public Guid DocumentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>
    /// 事件发生时间。下游消费方按 <c>(DocumentId, EventType, EventTime)</c> 做幂等。
    /// </summary>
    public DateTime EventTime { get; set; }

    public string? DocumentTypeCode { get; set; }

    /// <summary>
    /// 最终 OCR 置信度（0.0 - 1.0）。仅 OCR 路径有值；数字版抽取无 OCR 概念，此值为 <c>null</c>。
    /// 下游不应把 null 当 1.0 处理——文档生命周期已经过 OCR 门槛或人工审核才到 Ready，
    /// 置信度数值是给操作员 UI / 次级 quality gating 的辅助信号，不是路径判别依据。
    /// </summary>
    public double? OcrConfidence { get; set; }
}
