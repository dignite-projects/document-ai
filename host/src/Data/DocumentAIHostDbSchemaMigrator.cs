using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;

namespace Dignite.DocumentAI.Host.Data;

public class DocumentAIHostDbSchemaMigrator : ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public DocumentAIHostDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolve DbContexts from IServiceProvider
         * to properly get the connection string of the current tenant
         * in the current scope.
         */

        await _serviceProvider
            .GetRequiredService<DocumentAIHostDbContext>()
            .Database
            .MigrateAsync();
    }
}
