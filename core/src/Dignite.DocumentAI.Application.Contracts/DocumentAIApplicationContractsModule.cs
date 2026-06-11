using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace Dignite.DocumentAI;

[DependsOn(
    typeof(DocumentAIDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class DocumentAIApplicationContractsModule : AbpModule
{

}
