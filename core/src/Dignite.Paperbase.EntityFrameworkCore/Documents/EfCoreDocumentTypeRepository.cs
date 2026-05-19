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

public class EfCoreDocumentTypeRepository
    : EfCoreRepository<PaperbaseDbContext, DocumentType, Guid>, IDocumentTypeRepository
{
    public EfCoreDocumentTypeRepository(IDbContextProvider<PaperbaseDbContext> dbContextProvider)
        : base(dbContextProvider) { }

    public async Task<List<DocumentType>> GetVisibleAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(t => t.TenantId == null || t.TenantId == tenantId)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.TypeCode)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<DocumentType?> FindByTypeCodeAsync(
        Guid? tenantId,
        string typeCode,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(
            t => (t.TenantId == null || t.TenantId == tenantId) && t.TypeCode == typeCode,
            GetCancellationToken(cancellationToken));
    }
}
