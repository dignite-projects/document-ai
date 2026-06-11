using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.DocumentAI.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.DocumentAI.Documents.DocumentTypes;

public class EfCoreDocumentTypeRepository
    : EfCoreRepository<DocumentAIDbContext, DocumentType, Guid>, IDocumentTypeRepository
{
    public EfCoreDocumentTypeRepository(IDbContextProvider<DocumentAIDbContext> dbContextProvider)
        : base(dbContextProvider) { }

    public async Task<DocumentType?> FindByTypeCodeAsync(
        string typeCode,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(
            t => t.TypeCode == typeCode,
            GetCancellationToken(cancellationToken));
    }
}
