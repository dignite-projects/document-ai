using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dignite.DocumentAI.Documents.Exports;

public class ExportTemplateDto : EntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = default!;
    public ExportFormat Format { get; set; }

    /// <summary>限定的文档类型不可变 Id（#207：内部稳定句柄，TypeCode 可由 admin 重命名故不作引用键）。</summary>
    public Guid DocumentTypeId { get; set; }
    public List<ExportColumnDto> Columns { get; set; } = new();
}
