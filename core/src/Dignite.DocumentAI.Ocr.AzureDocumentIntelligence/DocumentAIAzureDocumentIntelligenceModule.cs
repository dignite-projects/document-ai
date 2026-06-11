using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.DocumentAI.Ocr.AzureDocumentIntelligence;

[DependsOn(typeof(DocumentAIOcrModule))]
public class DocumentAIAzureDocumentIntelligenceModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.Configure<AzureDocumentIntelligenceOptions>(
            configuration.GetSection("AzureDocumentIntelligence"));
    }
}
