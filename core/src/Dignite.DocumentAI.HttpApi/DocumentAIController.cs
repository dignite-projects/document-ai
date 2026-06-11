using Dignite.DocumentAI.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.DocumentAI;

public abstract class DocumentAIController : AbpControllerBase
{
    protected DocumentAIController()
    {
        LocalizationResource = typeof(DocumentAIResource);
    }
}
