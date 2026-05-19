using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Paperbase.Documents;

public class EfCoreFieldDefinitionRepository
    : EfCoreRepository<PaperbaseDbContext, FieldDefinition, Guid>, IFieldDefinitionRepository
{
    public EfCoreFieldDefinitionRepository(IDbContextProvider<PaperbaseDbContext> dbContextProvider)
        : base(dbContextProvider) { }

    public async Task<List<FieldDefinition>> GetForExtractionAsync(
        Guid? tenantId,
        string documentTypeCode,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        // 解读 X：按 tenantId 精确匹配单层。NULL-safe equality 用分支避免 EF Core
        // 对 nullable Guid? == nullable Guid? 翻译歧义。
        var query = tenantId.HasValue
            ? dbSet.Where(f => f.TenantId == tenantId.Value && f.DocumentTypeCode == documentTypeCode)
            : dbSet.Where(f => f.TenantId == null && f.DocumentTypeCode == documentTypeCode);

        return await query
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<FieldDefinition>> GetByDocumentTypeAsync(
        Guid? tenantId,
        string documentTypeCode,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var query = tenantId.HasValue
            ? dbSet.Where(f => f.TenantId == tenantId.Value && f.DocumentTypeCode == documentTypeCode)
            : dbSet.Where(f => f.TenantId == null && f.DocumentTypeCode == documentTypeCode);

        return await query
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<FieldDefinition?> FindByNameAsync(
        Guid? tenantId,
        string documentTypeCode,
        string name,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(
            f => f.TenantId == tenantId
              && f.DocumentTypeCode == documentTypeCode
              && f.Name == name,
            GetCancellationToken(cancellationToken));
    }
}
