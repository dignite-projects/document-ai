using System;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents.Reprocessing;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.Paperbase.HttpApi.Documents.Reprocessing;

// 手写 controller 显式暴露 IDocumentReprocessingAppService（#216：host Auto API 只覆盖 host assembly，
// Application assembly 的 AppService 靠 HttpApi 显式 controller 转发，否则前端调用落 404）。
[Area("paperbase")]
[Route("api/paperbase/document-reprocessing")]
public class DocumentReprocessingController : PaperbaseController, IDocumentReprocessingAppService
{
    private readonly IDocumentReprocessingAppService _appService;

    public DocumentReprocessingController(IDocumentReprocessingAppService appService)
    {
        _appService = appService;
    }

    // GET /api/paperbase/document-reprocessing/field-extraction/preview?documentTypeId=...
    [HttpGet("field-extraction/preview")]
    public virtual Task<FieldReextractionPreviewDto> PreviewFieldExtractionAsync(Guid documentTypeId)
    {
        return _appService.PreviewFieldExtractionAsync(documentTypeId);
    }

    // POST /api/paperbase/document-reprocessing/field-extraction
    [HttpPost("field-extraction")]
    public virtual Task<ReprocessingStartResultDto> StartFieldExtractionAsync([FromBody] StartFieldReextractionInput input)
    {
        return _appService.StartFieldExtractionAsync(input);
    }

    // POST /api/paperbase/document-reprocessing/reclassification/preview
    // 用 POST 承载范围对象（含枚举 + 可空类型 + 开关），避免 query string 表达复杂入参。
    [HttpPost("reclassification/preview")]
    public virtual Task<ReclassificationPreviewDto> PreviewReclassificationAsync([FromBody] ReclassificationScopeInput input)
    {
        return _appService.PreviewReclassificationAsync(input);
    }

    // POST /api/paperbase/document-reprocessing/reclassification
    [HttpPost("reclassification")]
    public virtual Task<ReprocessingStartResultDto> StartReclassificationAsync([FromBody] ReclassificationScopeInput input)
    {
        return _appService.StartReclassificationAsync(input);
    }
}
