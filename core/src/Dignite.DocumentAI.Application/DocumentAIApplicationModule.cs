using Dignite.DocumentAI.Abstractions;
using Dignite.DocumentAI.Ai;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace Dignite.DocumentAI;

[DependsOn(
    typeof(DocumentAIAbstractionsModule),
    typeof(DocumentAIDomainModule),
    typeof(DocumentAIApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBackgroundJobsModule),
    typeof(AbpMapperlyModule)
    )]
public class DocumentAIApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<DocumentAIApplicationModule>();

        var configuration = context.Services.GetConfiguration();
        Configure<DocumentAIBehaviorOptions>(configuration.GetSection("DocumentAIBehavior"));
    }
}
