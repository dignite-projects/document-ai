using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.DocumentAI.Permissions;

namespace Dignite.DocumentAI.Documents.Pipelines;

/// <summary>
/// <see cref="IDocumentPipelineRunAppService"/> 的实现（#216）。
/// 权限：显式 <c>CheckPolicyAsync(Documents.Default)</c> 与 <c>DocumentAppService</c> 同源（反射 / LLM tool 路径下 <c>[Authorize]</c> 不触发）。
/// 租户隔离：ABP <c>IMultiTenant</c> 全局过滤器自动施加。
/// </summary>
public class DocumentPipelineRunAppService : DocumentAIAppService, IDocumentPipelineRunAppService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentPipelineRunRepository _runRepository;
    private readonly DocumentPipelineRunToDocumentPipelineRunDtoMapper _runMapper;

    public DocumentPipelineRunAppService(
        IDocumentRepository documentRepository,
        IDocumentPipelineRunRepository runRepository,
        DocumentPipelineRunToDocumentPipelineRunDtoMapper runMapper)
    {
        _documentRepository = documentRepository;
        _runRepository = runRepository;
        _runMapper = runMapper;
    }

    public virtual async Task<List<DocumentPipelineRunDto>> GetListAsync(Guid documentId)
    {
        await CheckPolicyAsync(DocumentAIPermissions.Documents.Default);

        // Fail-closed 安全门：必须经文档读路径断言可见性，再返回其编排状态。仅 CheckPolicyAsync 不够——
        // PipelineRun 自身有 IMultiTenant 过滤但<b>不</b>实现 ISoftDelete（DB 级 CASCADE 才清行），
        // 软删 Document 的 runs 仍在 child 表里；若不经 Document.GetAsync 断言，调用方猜对一个本租户但
        // 已软删 / 未来加可见性规则隐藏的 documentId，就能从这个 endpoint 取到其编排元数据（orphan disclosure）。
        // GetAsync 经 ISoftDelete + IMultiTenant 双过滤器：找不到 → EntityNotFoundException → 404（与契约一致）。
        _ = await _documentRepository.GetAsync(documentId, includeDetails: false);

        var runs = await _runRepository.GetListByDocumentAsync(documentId);
        // 直接调子 mapper Map(source)（不经 ObjectMapper），让 AfterMap 触发 Candidates 解码——
        // 与原 [UseMapper] 嵌套路径行为一致（DocumentAIApplicationMappers 注释）。
        return runs.Select(_runMapper.Map).ToList();
    }
}
