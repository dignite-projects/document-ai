using System.Threading;
using System.Threading.Tasks;
using Dignite.DocumentAI.Slugging;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.DocumentAI.HttpApi.Slugging;

[Area("document-ai")]
[Route("api/document-ai/slug-suggestion")]
public class SlugSuggestionController : DocumentAIController, ISlugSuggestionAppService
{
    private readonly ISlugSuggestionAppService _slugSuggestionAppService;

    public SlugSuggestionController(ISlugSuggestionAppService slugSuggestionAppService)
    {
        _slugSuggestionAppService = slugSuggestionAppService;
    }

    [HttpPost("suggest")]
    public virtual Task<SlugSuggestionDto> SuggestAsync([FromBody] SuggestSlugInput input, CancellationToken cancellationToken)
    {
        return _slugSuggestionAppService.SuggestAsync(input, cancellationToken);
    }
}
