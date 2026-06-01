using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Paperbase.Documents.Pipelines;

/// <summary>
/// <see cref="DocumentPipelineRunDto"/> 出口（#216：从 <c>DocumentDto.PipelineRuns</c> 拆出来）。
/// 第一版仅暴露"按 documentId 列指定文档的全部 run"——前端文档详情页流水线状态行的最小依赖。
/// 未来按需扩展跨文档查询面（运维诊断面板：按 status 过滤 failed runs、按时间窗口聚合等）。
/// <para>
/// 路由由 ABP Auto API Controllers 约定生成：单 Guid 参 → query string，
/// 即 <c>GET /api/paperbase/document-pipeline-runs?documentId=...</c>。
/// </para>
/// </summary>
public interface IDocumentPipelineRunAppService : IApplicationService
{
    /// <summary>
    /// 按 <paramref name="documentId"/> 返回该文档的全部流水线运行记录（按 (PipelineCode, AttemptNumber) 排序）。
    /// 权限：<c>PaperbasePermissions.Documents.Default</c>；
    /// 租户隔离由 ABP <c>IMultiTenant</c> 全局过滤器自动施加。
    /// 文档不存在或跨租户 → 抛 <c>EntityNotFoundException</c>。
    /// </summary>
    Task<List<DocumentPipelineRunDto>> GetListAsync(Guid documentId);
}
