using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.DocumentAI.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.DocumentAI.Documents.Cabinets;

public class EfCoreCabinetRepository
    : EfCoreRepository<DocumentAIDbContext, Cabinet, Guid>, ICabinetRepository
{
    public EfCoreCabinetRepository(IDbContextProvider<DocumentAIDbContext> dbContextProvider)
        : base(dbContextProvider) { }

    public async Task<Cabinet?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(
            c => c.Name == name,
            GetCancellationToken(cancellationToken));
    }
}
