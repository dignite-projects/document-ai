using System;

namespace Dignite.DocumentAI.Documents;

/// <summary>
/// 文档宏观生命周期状态变更的本地域事件。
/// 在 <see cref="Document.TransitionLifecycle"/> 内通过 AddLocalEvent 发布，与状态变更同事务。
/// <para>
/// 合理消费场景仅限 DocumentAI 通道层内部的同进程钩子：
/// <list type="bullet">
///   <item>跃迁到 <c>Ready</c> 时由 <c>DocumentReadyEventHandler</c> 发出 <c>DocumentReadyEto</c> 出口事件</item>
///   <item>跃迁到 <c>Failed</c> 或 <c>Ready</c> 时向操作员 UI 的 SignalR/SSE hub 推实时通知</item>
/// </list>
/// 业务副作用（用户通知 / 审批流 / 统计聚合）属下游消费方职责——订阅
/// <c>DocumentReadyEto</c> 等出口 ETO 在自己的进程内实现，不应挂在本地事件上。
/// </para>
/// </summary>
public class DocumentLifecycleStatusChangedEvent
{
    public Guid DocumentId { get; }
    public DocumentLifecycleStatus OldStatus { get; }
    public DocumentLifecycleStatus NewStatus { get; }

    public DocumentLifecycleStatusChangedEvent(
        Guid documentId,
        DocumentLifecycleStatus oldStatus,
        DocumentLifecycleStatus newStatus)
    {
        DocumentId = documentId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}
