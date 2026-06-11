using Volo.Abp.Application.Services;
using Dignite.DocumentAI.Host.Localization;

namespace Dignite.DocumentAI.Host.Services;

/* Inherit your application services from this class. */
public abstract class DocumentAIHostAppService : ApplicationService
{
    protected DocumentAIHostAppService()
    {
        LocalizationResource = typeof(DocumentAIHostResource);
    }
}
