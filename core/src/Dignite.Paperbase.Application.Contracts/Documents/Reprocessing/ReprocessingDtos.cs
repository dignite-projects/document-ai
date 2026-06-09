using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Paperbase.Documents.Reprocessing;

/// <summary>
/// 批量字段重抽预览（#289 场景二）：受影响文档数 + 该类型当前字段清单（让人知道「重抽哪些字段」）。
/// </summary>
public class FieldReextractionPreviewDto
{
    public Guid DocumentTypeId { get; set; }

    /// <summary>该类型下已完成文本提取、将被重抽的文档数（当前层、不含回收站）。</summary>
    public long DocumentCount { get; set; }

    /// <summary>该类型当前活跃字段定义名（按 DisplayOrder）——预览展示「将抽取哪些字段」。</summary>
    public List<string> FieldNames { get; set; } = new();
}

/// <summary>批量字段重抽触发入参：固定按文档类型范围（叶子操作，无级联、无破坏性分类副作用）。</summary>
public class StartFieldReextractionInput
{
    [Required]
    public Guid DocumentTypeId { get; set; }
}

/// <summary>批量重新分类预览（#289 场景一）：受影响文档数。重警告文案由前端按范围 + 开关组合呈现。</summary>
public class ReclassificationPreviewDto
{
    public long DocumentCount { get; set; }
}

/// <summary>
/// 批量重新分类的范围入参（预览 / 触发共用）。范围由人选、系统不预设默认。
/// <para>
/// 校验：<see cref="ReclassificationScope.OnlyCurrentType"/> 必须带 <see cref="DocumentTypeId"/>；
/// 其余范围忽略 <see cref="DocumentTypeId"/>。
/// </para>
/// </summary>
public class ReclassificationScopeInput : IValidatableObject
{
    [Required]
    public ReclassificationScope Scope { get; set; }

    /// <summary>仅 <see cref="ReclassificationScope.OnlyCurrentType"/> 必填——锚定「仅已归为该类型」的文档。</summary>
    public Guid? DocumentTypeId { get; set; }

    /// <summary>
    /// 是否连「已人工确认（<see cref="DocumentReviewDisposition.Confirmed"/>）」的文档也重分。
    /// 默认 <c>false</c>（保护人工确认，#289 默认开启）——把「覆盖人工成果」变成显式 opt-in。
    /// 对 <see cref="ReclassificationScope.PendingReviewQueue"/> 无意义（待审核文档本就未确认）。
    /// </summary>
    public bool IncludeManuallyConfirmed { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Scope == ReclassificationScope.OnlyCurrentType && !DocumentTypeId.HasValue)
        {
            yield return new ValidationResult(
                "DocumentTypeId is required when Scope is OnlyCurrentType.",
                new[] { nameof(DocumentTypeId) });
        }
    }
}

/// <summary>
/// 批量重处理触发结果：本次预估入队的文档数（触发时刻 count 查询的快照——dispatcher 链式枚举为最终事实源，
/// 故为「预估」；批次 / 进度是内部运维状态，不进出口契约）。
/// </summary>
public class ReprocessingStartResultDto
{
    public long EstimatedDocumentCount { get; set; }
}
