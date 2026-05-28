using System.ComponentModel.DataAnnotations;
using Dignite.Paperbase.Documents.Fields;
using Volo.Abp.Validation;

namespace Dignite.Paperbase.Documents.Exports;

/// <summary>
/// 导出模板列入参（#207 收敛为 ExtractedField-only）。提交字段名，AppService 按模板 <c>DocumentTypeCode</c> 解析到
/// <c>FieldDefinitionId</c> 持久化。系统字段固定导出，不在此配置。
/// </summary>
public class ExportColumnInput
{
    /// <summary>要导出的类型绑定字段名（必须是该模板文档类型下已定义的字段）。</summary>
    [Required]
    [RegularExpression(FieldDefinitionConsts.NamePattern)]
    [DynamicStringLength(typeof(FieldDefinitionConsts), nameof(FieldDefinitionConsts.MaxNameLength))]
    public string FieldName { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(ExportTemplateConsts), nameof(ExportTemplateConsts.MaxColumnNameLength))]
    public string ColumnName { get; set; } = default!;

    public int Order { get; set; }
}
