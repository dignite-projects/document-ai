using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.DocumentAI.Documents;

/// <summary>
/// Operator overview statistics (#333): a small read-only aggregate over the current layer's documents,
/// surfaced on the Document AI overview home. Kept separate from <see cref="IDocumentAppService"/> so that
/// service stays focused. Gated by <c>DocumentAIPermissions.Documents.Default</c> (same as the list).
/// </summary>
public interface IDocumentStatisticsAppService : IApplicationService
{
    Task<DocumentStatisticsDto> GetAsync();
}
