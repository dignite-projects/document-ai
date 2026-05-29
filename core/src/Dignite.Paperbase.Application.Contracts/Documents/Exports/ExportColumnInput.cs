using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace Dignite.Paperbase.Documents.Exports;

/// <summary>
/// 导出模板列入参（#207 收敛为 ExtractedField-only）。提交字段定义不可变 Id，AppService 校验其属于模板的
/// <c>DocumentTypeId</c> 后持久化。系统字段固定导出，不在此配置。
/// </summary>
public class ExportColumnInput
{
    /// <summary>要导出的类型绑定字段定义不可变 Id（必须属于该模板的文档类型）。</summary>
    [Required]
    public Guid FieldDefinitionId { get; set; }

    [Required]
    [DynamicStringLength(typeof(ExportTemplateConsts), nameof(ExportTemplateConsts.MaxColumnNameLength))]
    public string ColumnName { get; set; } = default!;

    public int Order { get; set; }
}
