using Dignite.DocumentAI.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.DocumentAI;

public abstract class DocumentAIAppService : ApplicationService
{
    protected DocumentAIAppService()
    {
        LocalizationResource = typeof(DocumentAIResource);
        ObjectMapperContext = typeof(DocumentAIApplicationModule);
    }
}
