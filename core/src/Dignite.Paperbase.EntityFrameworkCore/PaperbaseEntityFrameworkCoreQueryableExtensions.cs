using System.Linq;
using Dignite.Paperbase.Documents;
using Microsoft.EntityFrameworkCore;

namespace Dignite.Paperbase;

public static class PaperbaseEntityFrameworkCoreQueryableExtensions
{
    public static IQueryable<Document> IncludeDetails(
        this IQueryable<Document> queryable,
        bool include = true)
    {
        if (!include)
        {
            return queryable;
        }

        // 仅剩 ExtractedFieldValues 一个集合 child（#206）；单一 Include，无笛卡尔积风险，AsSplitQuery 已不必要。
        // PipelineRuns 自 #216 拆为独立聚合根后不在此 eager-load——查询走 IDocumentPipelineRunRepository。
        return queryable.Include(x => x.ExtractedFieldValues);
    }
}
