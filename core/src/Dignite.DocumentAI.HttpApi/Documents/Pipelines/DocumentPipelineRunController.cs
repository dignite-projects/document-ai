using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.DocumentAI.Documents.Pipelines;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.DocumentAI.HttpApi.Documents.Pipelines;

// #216 把 PipelineRun 拆为独立聚合根并新增 IDocumentPipelineRunAppService，但当时漏了这个手写 controller。
// host Auto API 只覆盖 DocumentAIHostModule.Assembly（见 DocumentAIHostModule.ConfigureAutoApiControllers），
// Application assembly 的 AppService 全靠 HttpApi 显式 controller 暴露——缺这层转发，前端调
// /api/document-ai/document-pipeline-runs 会落到无匹配路由的 404（null body），拖垮文档详情页的 forkJoin。
[Area("document-ai")]
[Route("api/document-ai/document-pipeline-runs")]
public class DocumentPipelineRunController : DocumentAIController, IDocumentPipelineRunAppService
{
    private readonly IDocumentPipelineRunAppService _documentPipelineRunAppService;

    public DocumentPipelineRunController(IDocumentPipelineRunAppService documentPipelineRunAppService)
    {
        _documentPipelineRunAppService = documentPipelineRunAppService;
    }

    // GET /api/document-ai/document-pipeline-runs?documentId=...
    // 单个 Guid 简单参数在 GET 下默认从 query string 绑定，与前端 proxy（params: { documentId }）对齐。
    [HttpGet]
    public virtual Task<List<DocumentPipelineRunDto>> GetListAsync(Guid documentId)
    {
        return _documentPipelineRunAppService.GetListAsync(documentId);
    }
}
