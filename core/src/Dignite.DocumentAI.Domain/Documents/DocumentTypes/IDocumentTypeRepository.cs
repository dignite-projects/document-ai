using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.DocumentAI.Documents.DocumentTypes;

public interface IDocumentTypeRepository : IRepository<DocumentType, Guid>
{
    Task<DocumentType?> FindByTypeCodeAsync(
        string typeCode,
        CancellationToken cancellationToken = default);
}
