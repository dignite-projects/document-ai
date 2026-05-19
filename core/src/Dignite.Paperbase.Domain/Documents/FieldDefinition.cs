using System;
using Dignite.Paperbase.Abstractions.Documents;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Paperbase.Documents;

/// <summary>
/// 字段定义实体（字段架构 v2）。统一承载 Host 字段 + 租户字段：
/// <list type="bullet">
///   <item><c>TenantId == null</c> → Host 部署级字段（由 HostDocumentTypeDataSeedContributor 种子化）</item>
///   <item><c>TenantId != null</c> → 租户私有字段（租户管理员通过 IFieldDefinitionAppService 自助 CRUD）</item>
/// </list>
/// 唯一约束 <c>(TenantId, DocumentTypeCode, Name)</c>：同一租户同一类型下字段名不重复。
/// <para>
/// <see cref="DocumentTypeCode"/> 字符串引用 <see cref="DocumentType.TypeCode"/>，按 DDD reference-by-id 原则不加 navigation。
/// </para>
/// <para>
/// 安全约束（CLAUDE.md "## 安全约定"）：
/// <list type="bullet">
///   <item>所有查询路径显式 TenantId 谓词</item>
///   <item><see cref="Prompt"/> 是用户控制文本，LLM 抽取时由 Workflow 经 <c>PromptBoundary.WrapField</c> 包裹</item>
/// </list>
/// </para>
/// </summary>
public class FieldDefinition : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; private set; }

    public virtual string DocumentTypeCode { get; private set; } = default!;

    public virtual string Name { get; private set; } = default!;

    /// <summary>LLM 抽取指令——告诉模型从文档中找什么值。</summary>
    public virtual string Prompt { get; private set; } = default!;

    public virtual FieldDataType DataType { get; private set; }

    public virtual int DisplayOrder { get; private set; }

    public virtual bool IsRequired { get; private set; }

    protected FieldDefinition() { }

    public FieldDefinition(
        Guid id,
        Guid? tenantId,
        string documentTypeCode,
        string name,
        string prompt,
        FieldDataType dataType,
        int displayOrder = 0,
        bool isRequired = false)
        : base(id)
    {
        TenantId = tenantId;
        DocumentTypeCode = Check.NotNullOrWhiteSpace(documentTypeCode, nameof(documentTypeCode), FieldDefinitionConsts.MaxDocumentTypeCodeLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), FieldDefinitionConsts.MaxNameLength);
        Prompt = Check.NotNullOrWhiteSpace(prompt, nameof(prompt), FieldDefinitionConsts.MaxPromptLength);
        DataType = dataType;
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
    }

    public void Update(string prompt, FieldDataType dataType, int displayOrder, bool isRequired)
    {
        Prompt = Check.NotNullOrWhiteSpace(prompt, nameof(prompt), FieldDefinitionConsts.MaxPromptLength);
        DataType = dataType;
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
    }
}
