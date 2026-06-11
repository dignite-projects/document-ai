using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dignite.DocumentAI.Host.Data;

public class DocumentAIHostDbContextFactory : IDesignTimeDbContextFactory<DocumentAIHostDbContext>
{
    public DocumentAIHostDbContext CreateDbContext(string[] args)
    {
        DocumentAIHostGlobalFeatureConfigurator.Configure();
        DocumentAIHostModuleExtensionConfigurator.Configure();

        DocumentAIHostEfCoreEntityExtensionMappings.Configure();
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<DocumentAIHostDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));

        return new DocumentAIHostDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}
