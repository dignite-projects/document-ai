using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Paperbase.Documents;

public interface IDocumentTypeRepository : IRepository<DocumentType, Guid>
{
    /// <summary>
    /// 当前租户可见的文档类型集合 = Host 类型（TenantId IS NULL）∪ 当前租户私有类型。
    /// 用于分类候选集组装。显式 TenantId 谓词不依赖 ambient DataFilter（安全约定：fail-closed）。
    /// </summary>
    Task<List<DocumentType>> GetVisibleAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    Task<DocumentType?> FindByTypeCodeAsync(
        Guid? tenantId,
        string typeCode,
        CancellationToken cancellationToken = default);
}
