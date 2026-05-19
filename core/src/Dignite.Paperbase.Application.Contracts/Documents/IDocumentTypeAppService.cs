using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Paperbase.Documents;

/// <summary>
/// 文档类型管理（字段架构 v2）。
/// <para>
/// 返回的列表是当前租户可见的合集（Host 类型 ∪ 当前租户私有类型）；
/// 创建 / 修改 / 删除只作用于当前租户私有类型（TenantId == CurrentTenant.Id）。
/// Host 类型（TenantId IS NULL）通过 IDataSeedContributor 维护，不通过此 AppService 改。
/// </para>
/// </summary>
public interface IDocumentTypeAppService : IApplicationService
{
    Task<List<DocumentTypeDto>> GetVisibleAsync();

    /// <summary>
    /// 当前租户已软删除的私有文档类型列表（回收站视图）。
    /// 不含 Host 类型——Host 类型由 IDataSeedContributor 维护，不参与租户级回收站。
    /// </summary>
    Task<List<DocumentTypeDto>> GetDeletedAsync();

    Task<DocumentTypeDto> CreateAsync(CreateDocumentTypeDto input);

    Task<DocumentTypeDto> UpdateAsync(Guid id, UpdateDocumentTypeDto input);

    Task DeleteAsync(Guid id);

    /// <summary>
    /// 恢复软删除的文档类型，并级联恢复同 (TenantId, TypeCode) 下随之被软删除的字段定义。
    /// 若同代码已有活跃记录则抛 <see cref="PaperbaseErrorCodes.DocumentTypeRestoreConflict"/>；
    /// 个别字段恢复时与活跃字段冲突的会被跳过（防御性，正常流程下不会发生）。
    /// </summary>
    Task<DocumentTypeDto> RestoreAsync(Guid id);
}
