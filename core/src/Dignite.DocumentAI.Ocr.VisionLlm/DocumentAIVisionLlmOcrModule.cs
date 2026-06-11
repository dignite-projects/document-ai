using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.DocumentAI.Ocr.VisionLlm;

[DependsOn(typeof(DocumentAIOcrModule))]
public class DocumentAIVisionLlmOcrModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.Configure<VisionLlmOcrOptions>(
            configuration.GetSection("VisionLlmOcr"));
    }
}
