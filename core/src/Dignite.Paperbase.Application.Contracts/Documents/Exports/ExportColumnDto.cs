using System;

namespace Dignite.Paperbase.Documents.Exports;

/// <summary>
/// 导出模板的一列（#207 收敛为 ExtractedField-only）。系统字段（SourceType / LifecycleStatus / ReviewStatus / Title）
/// 由导出引擎固定输出，不在此列出。
/// </summary>
public class ExportColumnDto
{
    /// <summary>引用的类型绑定字段定义 Id（内部不可变关联，#207）。</summary>
    public Guid FieldDefinitionId { get; set; }

    /// <summary>当前字段名（服务端 join 当前 <c>FieldDefinition</c> 解析，穿透 soft-delete——已归档字段也可读）。</summary>
    public string? FieldName { get; set; }

    /// <summary>输出文件中的列标题。</summary>
    public string ColumnName { get; set; } = default!;

    /// <summary>列在输出中的排序（升序）。</summary>
    public int Order { get; set; }
}
