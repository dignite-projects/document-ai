namespace Dignite.DocumentAI.Documents.Pipelines.FieldExtraction;

/// <summary>
/// <see cref="FieldExtractionService.ExtractAsync"/> 的结果——供调用方（事件 handler / 后台作业）
/// 记可观测日志。抽取本身的 ETO 发布 / 字段持久化已在引擎内完成，调用方无需据此再写库。
/// </summary>
public enum FieldExtractionOutcome
{
    /// <summary>前置守卫不满足（文档缺失 / 跨租户 / 未分类 / stale / 飞行期间被 reclassify）——未写未发。</summary>
    Skipped,

    /// <summary>目标类型无字段定义——清空残留字段行并发空 <c>FieldsExtractedEto</c>。</summary>
    Cleared,

    /// <summary>正常抽取——整组写入字段值并发 <c>FieldsExtractedEto</c>。</summary>
    Extracted
}

public readonly record struct FieldExtractionResult(FieldExtractionOutcome Outcome, int FieldCount)
{
    public static readonly FieldExtractionResult Skipped = new(FieldExtractionOutcome.Skipped, 0);
    public static readonly FieldExtractionResult Cleared = new(FieldExtractionOutcome.Cleared, 0);
    public static FieldExtractionResult Extracted(int fieldCount) => new(FieldExtractionOutcome.Extracted, fieldCount);
}
