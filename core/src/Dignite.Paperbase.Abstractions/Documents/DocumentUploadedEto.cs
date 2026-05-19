using System;
using Volo.Abp.EventBus;

namespace Dignite.Paperbase.Abstractions.Documents;

/// <summary>
/// 文档上传完成（落库 + Blob 落盘）后发布；处于通道流水线起点。
/// 薄载荷：下游通过 REST / MCP 回拉详细数据。不受置信度门槛约束。
/// </summary>
[EventName("Paperbase.Document.Uploaded")]
public class DocumentUploadedEto
{
    public string Version { get; set; } = "1.0";

    public Guid DocumentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>
    /// 事件发生时间——Paperbase 在 publish 时填入 <see cref="Volo.Abp.Timing.IClock.Now"/>。
    /// 下游消费方按 <c>(DocumentId, EventType, EventTime)</c> 做幂等：
    /// 同一 key 下若已处理过更晚的 EventTime，则丢弃。
    /// 配合 ABP 内置 transactional outbox 的 at-least-once 投递使用。
    /// </summary>
    public DateTime EventTime { get; set; }

    public string? FileName { get; set; }

    public long FileSize { get; set; }

    public string? ContentType { get; set; }
}
