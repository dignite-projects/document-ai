using System.Threading;
using System.Threading.Tasks;
using Dignite.DocumentAI.Documents.Fields;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.DocumentAI.HttpApi.Documents.Fields;

[Area("document-ai")]
[Route("api/document-ai/field-draft-suggestion")]
public class FieldDraftSuggestionController : DocumentAIController, IFieldDraftSuggestionAppService
{
    private readonly IFieldDraftSuggestionAppService _fieldDraftSuggestionAppService;

    public FieldDraftSuggestionController(IFieldDraftSuggestionAppService fieldDraftSuggestionAppService)
    {
        _fieldDraftSuggestionAppService = fieldDraftSuggestionAppService;
    }

    [HttpPost("draft")]
    public virtual Task<FieldDefinitionDraftDto> DraftAsync([FromBody] DraftFieldDefinitionInput input, CancellationToken cancellationToken)
    {
        return _fieldDraftSuggestionAppService.DraftAsync(input, cancellationToken);
    }
}
