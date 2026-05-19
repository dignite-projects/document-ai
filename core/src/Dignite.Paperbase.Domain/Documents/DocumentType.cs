using System;
using Dignite.Paperbase.Abstractions.Documents;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Paperbase.Documents;

/// <summary>
/// 文档类型实体（字段架构 v2）。统一承载两层文档类型体系：
/// <list type="bullet">
///   <item><c>TenantId == null</c> → Host 部署级类型（由 HostDocumentTypeDataSeedContributor 种子化）</item>
///   <item><c>TenantId != null</c> → 租户私有类型（租户管理员通过 IDocumentTypeAppService 自助 CRUD）</item>
/// </list>
/// <para>
/// TypeCode 全局唯一格式：<c>&lt;owner-module&gt;.&lt;sub-type&gt;</c>，由 <see cref="ValidateTypeCode"/> 强制。
/// 例：<c>host.general</c>、<c>host.contract</c>、<c>tenant.case-file</c>。
/// </para>
/// <para>
/// 字段关系：<see cref="FieldDefinition.DocumentTypeCode"/> 字符串引用本实体的 TypeCode，
/// 按 DDD "reference by id" 原则不加 navigation property。
/// </para>
/// </summary>
public class DocumentType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; private set; }

    public virtual string TypeCode { get; private set; } = default!;

    /// <summary>显示名称（运行时直接展示）。Host 类型由种子化时按默认 culture 解析 ILocalizableString 后存入。</summary>
    public virtual string DisplayName { get; private set; } = default!;

    /// <summary>分类置信度阈值（低于此值进入 PendingReview 队列）。</summary>
    public virtual double ConfidenceThreshold { get; private set; }

    /// <summary>类型匹配优先级（数字越大优先级越高；Host fallback 通常为 0）。</summary>
    public virtual int Priority { get; private set; }

    protected DocumentType() { }

    public DocumentType(
        Guid id,
        Guid? tenantId,
        string typeCode,
        string displayName,
        double confidenceThreshold = ClassificationDefaults.DefaultConfidenceThreshold,
        int priority = 0)
        : base(id)
    {
        TenantId = tenantId;
        TypeCode = ValidateTypeCode(typeCode);
        DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), DocumentTypeConsts.MaxDisplayNameLength);
        ConfidenceThreshold = Check.Range(confidenceThreshold, nameof(confidenceThreshold), 0d, 1d);
        Priority = priority;
    }

    public void Update(string displayName, double confidenceThreshold, int priority)
    {
        DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), DocumentTypeConsts.MaxDisplayNameLength);
        ConfidenceThreshold = Check.Range(confidenceThreshold, nameof(confidenceThreshold), 0d, 1d);
        Priority = priority;
    }

    private static string ValidateTypeCode(string typeCode)
    {
        Check.NotNullOrWhiteSpace(typeCode, nameof(typeCode), DocumentTypeConsts.MaxTypeCodeLength);

        var dotIndex = typeCode.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == typeCode.Length - 1)
        {
            throw new ArgumentException(
                $"TypeCode must follow the '<owner-module>.<sub-type>' convention (e.g. 'host.general'). Got: '{typeCode}'.",
                nameof(typeCode));
        }

        return typeCode;
    }
}
