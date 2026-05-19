using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Paperbase.Documents;

public interface IFieldDefinitionRepository : IRepository<FieldDefinition, Guid>
{
    /// <summary>
    /// 该文档应该用于抽取的字段定义。
    /// 解读 X：按文档所属租户精确匹配字段定义层——Host 文档（tenantId IS NULL）用 Host 字段；
    /// 租户文档用对应租户字段。两层 mutually exclusive 不混。
    /// </summary>
    Task<List<FieldDefinition>> GetForExtractionAsync(
        Guid? tenantId,
        string documentTypeCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按租户视图查询某文档类型下的字段定义（仅当前层；管理 UI 列表用）。
    /// </summary>
    Task<List<FieldDefinition>> GetByDocumentTypeAsync(
        Guid? tenantId,
        string documentTypeCode,
        CancellationToken cancellationToken = default);

    Task<FieldDefinition?> FindByNameAsync(
        Guid? tenantId,
        string documentTypeCode,
        string name,
        CancellationToken cancellationToken = default);
}
