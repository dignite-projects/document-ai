using Volo.Abp.Modularity;

namespace Dignite.DocumentAI;

[DependsOn(
    typeof(DocumentAIApplicationModule),
    typeof(DocumentAIDomainTestModule)
    )]
public class DocumentAIApplicationTestModule : AbpModule
{
}
