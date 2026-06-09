namespace Dignite.Paperbase.Documents;

/// <summary>
/// 待审原因的<b>阻断性</b>策略——单点声明哪些 <see cref="DocumentReviewReasons"/> 是 blocking（阻断 Ready 闸门）。
/// <para>
/// <c>[Flags]</c> 成员无法挂元数据，故把 blocking 集合做成一个可按位与的常量：Ready 闸门
/// （<c>DocumentPipelineRunManager.DeriveLifecycleAsync</c>）与未来新增原因都只改这一处。
/// 新增 blocking 原因 = OR 进 <see cref="Blocking"/>；新增 non-blocking 原因 = 不动它。
/// </para>
/// </summary>
public static class ReviewReasonPolicy
{
    /// <summary>阻断 Ready 的原因集合。含任一则文档对下游不可用。</summary>
    public const DocumentReviewReasons Blocking = DocumentReviewReasons.UnresolvedClassification;

    /// <summary>是否含任一 blocking 原因（Ready 闸门判据）。</summary>
    public static bool HasBlocking(DocumentReviewReasons reasons) => (reasons & Blocking) != DocumentReviewReasons.None;

    /// <summary>是否含任一未解决原因（"是否需操作员关注" / 审核队列判据）。</summary>
    public static bool RequiresAttention(DocumentReviewReasons reasons) => reasons != DocumentReviewReasons.None;

    /// <summary>
    /// 单个原因是否 blocking——供出口 DTO 的 <c>IsBlocking</c> 投影（服务端按 policy 填，客户端不再自行判断）。
    /// </summary>
    public static bool IsBlocking(DocumentReviewReasons reason) => (reason & Blocking) != DocumentReviewReasons.None;
}
