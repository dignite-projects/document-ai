using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace Dignite.Paperbase.Documents.Exports;

public class UpdateExportTemplateDto
{
    [Required]
    [DynamicStringLength(typeof(ExportTemplateConsts), nameof(ExportTemplateConsts.MaxNameLength))]
    public string Name { get; set; } = default!;

    public ExportFormat Format { get; set; } = ExportFormat.Csv;

    /// <summary>限定的文档类型不可变 Id（#207 收敛为 ExtractedField-only 列后必填——列引用该类型下的字段定义）。</summary>
    [Required]
    public Guid DocumentTypeId { get; set; }

    [Required]
    [MinLength(1)]
    public List<ExportColumnInput> Columns { get; set; } = new();
}
