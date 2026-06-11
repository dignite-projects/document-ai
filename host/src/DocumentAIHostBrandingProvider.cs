using Microsoft.Extensions.Localization;
using Dignite.DocumentAI.Host.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace Dignite.DocumentAI.Host;

[Dependency(ReplaceServices = true)]
public class DocumentAIHostBrandingProvider : DefaultBrandingProvider
{
    private readonly IStringLocalizer<DocumentAIHostResource> _localizer;

    public DocumentAIHostBrandingProvider(IStringLocalizer<DocumentAIHostResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
