using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.DocumentAI;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class DocumentAIInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DocumentAIInstallerModule>();
        });
    }
}
