using System.Threading.Tasks;
using Dignite.DocumentAI.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.DocumentAI.HttpApi.Documents;

[Area("document-ai")]
[Route("api/document-ai/document-statistics")]
public class DocumentStatisticsController : DocumentAIController, IDocumentStatisticsAppService
{
    private readonly IDocumentStatisticsAppService _documentStatisticsAppService;

    public DocumentStatisticsController(IDocumentStatisticsAppService documentStatisticsAppService)
    {
        _documentStatisticsAppService = documentStatisticsAppService;
    }

    [HttpGet]
    public virtual Task<DocumentStatisticsDto> GetAsync()
    {
        return _documentStatisticsAppService.GetAsync();
    }
}
