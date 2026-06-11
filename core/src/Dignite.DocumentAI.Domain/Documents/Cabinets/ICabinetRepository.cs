using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.DocumentAI.Documents.Cabinets;

public interface ICabinetRepository : IRepository<Cabinet, Guid>
{
    /// <summary>
    /// 当前层按柜名精确查找（用于 CRUD 判重）。只查活跃柜——Cabinet 无回收站，软删柜名可被新柜复用
    /// （唯一索引带 <c>IsDeleted = 0</c> 过滤）。
    /// </summary>
    Task<Cabinet?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
