namespace Dignite.Paperbase.Documents.Reprocessing;

/// <summary>
/// 批量重新分类的范围（#289 场景一）。范围由人按意图选、系统不预设默认——分类是「候选集竞争选一个」，
/// 改一个类型的描述等于改了整个分类函数，任何文档的判定都可能变。
/// </summary>
public enum ReclassificationScope
{
    /// <summary>
    /// 仅已归为指定类型的文档（局部修正：踢出不再符合的）。最便宜、破坏面局限；
    /// <b>死角</b>：捞不回「本该归此类、却被分到别处」的文档——纳入新文档须用 <see cref="AllDocuments"/>。
    /// 要求传 <c>DocumentTypeId</c>。
    /// </summary>
    OnlyCurrentType = 0,

    /// <summary>
    /// 全量 / 跨类型（既踢出又纳入）。<b>新增类型、归拢散落同类的唯一可行范围</b>——新类型没有任何「已归」文档。
    /// 最贵、破坏面最大（所有文档重新竞争），靠「保护人工确认 + 重警告」兜。
    /// </summary>
    AllDocuments = 10,

    /// <summary>待审核队列（捞回之前没分成功的，<see cref="DocumentReviewStatus.PendingReview"/>）。范围小、安全。</summary>
    PendingReviewQueue = 20
}
