using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.DocumentAI;

[DependsOn(
    typeof(DocumentAIApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class DocumentAIHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(DocumentAIApplicationContractsModule).Assembly,
            DocumentAIRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DocumentAIHttpApiClientModule>();
        });

    }
}
