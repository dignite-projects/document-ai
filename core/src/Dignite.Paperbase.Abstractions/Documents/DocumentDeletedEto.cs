using System;
using Volo.Abp.EventBus;

namespace Dignite.Paperbase.Abstractions.Documents;

[EventName("Paperbase.Document.Deleted")]
public class DocumentDeletedEto
{
    public Guid DocumentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>
    /// 事件发生时间。下游消费方按 <c>(DocumentId, EventType, EventTime)</c> 做幂等。
    /// </summary>
    public DateTime EventTime { get; set; }

    public string? DocumentTypeCode { get; set; }
}
