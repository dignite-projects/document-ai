using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents.Pipelines;
using Dignite.Paperbase.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Paperbase.Documents.Pipelines;

/// <summary>
/// <see cref="IDocumentPipelineRunRepository"/> 的 EF Core 实现（#216：拆于 Document 子集合）。
/// <c>IMultiTenant</c> 全局过滤器经 <see cref="EfCoreRepository{TDbContext,TEntity,TKey}.GetDbSetAsync"/>
/// 自动施加，无需手写 TenantId 谓词。
/// </summary>
public class EfCoreDocumentPipelineRunRepository
    : EfCoreRepository<PaperbaseDbContext, DocumentPipelineRun, Guid>, IDocumentPipelineRunRepository
{
    public EfCoreDocumentPipelineRunRepository(
        IDbContextProvider<PaperbaseDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<DocumentPipelineRun?> FindLatestByDocumentAndCodeAsync(
        Guid documentId,
        string pipelineCode,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(r => r.DocumentId == documentId && r.PipelineCode == pipelineCode)
            .OrderByDescending(r => r.AttemptNumber)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<Dictionary<string, DocumentPipelineRun>> GetLatestRunsByCodesAsync(
        Guid documentId,
        IReadOnlyCollection<string> pipelineCodes,
        CancellationToken cancellationToken = default)
    {
        if (pipelineCodes.Count == 0)
        {
            return new Dictionary<string, DocumentPipelineRun>();
        }

        var dbSet = await GetDbSetAsync();

        // 按 PipelineCode 取最大 AttemptNumber 的 run。EF Core 8+ 把
        // GroupBy(...).Select(g => g.OrderByDescending(...).First()) 翻译为 SQL Server ROW_NUMBER() OVER
        // (PARTITION BY PipelineCode ORDER BY AttemptNumber DESC) WHERE rn = 1，
        // 只回传"每 code 一行"——不靠"少量行 + 内存 GroupBy"的假设。
        var latest = await dbSet
            .Where(r => r.DocumentId == documentId && pipelineCodes.Contains(r.PipelineCode))
            .GroupBy(r => r.PipelineCode)
            .Select(g => g.OrderByDescending(r => r.AttemptNumber).First())
            .ToListAsync(GetCancellationToken(cancellationToken));

        return latest.ToDictionary(r => r.PipelineCode);
    }

    public virtual async Task<List<DocumentPipelineRun>> GetListByDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(r => r.DocumentId == documentId)
            .OrderBy(r => r.PipelineCode)
            .ThenBy(r => r.AttemptNumber)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task DetachAsync(
        DocumentPipelineRun entity,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        dbContext.Entry(entity).State = EntityState.Detached;
    }
}
