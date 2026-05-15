using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents;
using Volo.Abp.DependencyInjection;

namespace Dignite.Paperbase.Contracts;

/// <summary>
/// L2 RelationDiscovery contributor for the contracts module — emits and looks up
/// contract numbers so two documents sharing the same contract number get connected
/// in the document graph.
///
/// <para>
/// <strong>The "contract number" type identifier is owned by THIS module</strong> — see
/// <see cref="ContractNumberTypeId"/>. Other business modules that want to interoperate
/// with contracts (e.g. an invoice module emitting "this invoice references contract
/// HT-2024-001") should NuGet-reference this module's Domain assembly and use that
/// constant. The Paperbase core layer carries no vocabulary of identifier type strings —
/// see <c>docs/relation-discovery-module-integration.md</c> for the full integration
/// contract.
/// </para>
///
/// <para>
/// <strong>Single source of truth</strong>: this provider reads <c>Contract</c> aggregate
/// fields directly. When a user corrects an AI-extracted value in the contract detail
/// page, the next L2 run picks up the new value immediately — no separate identifier
/// index to keep in sync.
/// </para>
///
/// <para>
/// <strong>Multi-tenancy</strong>: repository queries flow through ABP's <c>IMultiTenant</c>
/// ambient filter; the RelationDiscovery background job sets
/// <c>CurrentTenant.Change(args.TenantId)</c> before invoking us, so tenant scoping
/// is automatic.
/// </para>
/// </summary>
public class ContractIdentifierProvider : IDocumentIdentifierProvider, ITransientDependency
{
    /// <summary>
    /// Cross-module convention string for "this document holds (or references) a
    /// contract number". Other modules wishing to interoperate with the contracts
    /// module — emit/lookup the same type — should reference this constant via a
    /// NuGet PackageReference rather than hard-coding the literal <c>"ContractNumber"</c>.
    /// See <c>docs/relation-discovery-module-integration.md</c>.
    /// </summary>
    public const string ContractNumberTypeId = "ContractNumber";

    /// <summary>
    /// The contracts module exposes only <see cref="ContractNumberTypeId"/> on the
    /// single-field identifier path. Party names are intentionally absent here — a
    /// single vendor commonly owns hundreds of contracts, so PartyName as a standalone
    /// matcher generates a noise graph. Party-based matching is delivered via
    /// <see cref="ContractEntitySignatureProvider"/>'s multi-field
    /// <c>Contracts.PartiesAndYear</c> signature instead.
    /// </summary>
    public IReadOnlyCollection<string> SupportedIdentifierTypes { get; } = new[]
    {
        ContractNumberTypeId,
    };

    private readonly IContractRepository _contractRepository;

    public ContractIdentifierProvider(IContractRepository contractRepository)
    {
        _contractRepository = contractRepository;
    }

    public virtual async Task<IReadOnlyList<DocumentIdentifierEntry>> GetIdentifiersAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var contract = await _contractRepository.FindByDocumentIdAsync(documentId);
        if (contract == null)
        {
            // 该文档不属于合同模块（或合同记录尚未创建）。返回空表示"无贡献"，
            // L2 RelationDiscoveryService 会继续遍历其他 provider。
            return Array.Empty<DocumentIdentifierEntry>();
        }

        var entries = new List<DocumentIdentifierEntry>();
        // ContractNumber: emit raw display form + provider-computed normalized comparison key
        // (Issue #159 open contract — normalization is the provider's responsibility, not L2's).
        AddIfPresent(entries, ContractNumberTypeId, contract.ContractNumber);
        // PartyName intentionally NOT emitted (codex review fix [high]).
        // 一家供应商可能有上百份合同；以 PartyName 为 L2 强标识符会形成高置信度假关系图，
        // 污染 chat 路径的 cross-document 推理。Party 关系判断留给 L3 LLM /
        // ContractEntitySignatureProvider 的多字段签名路径。
        return entries;
    }

    /// <summary>
    /// The <paramref name="normalizedIdentifierValue"/> is the comparison key sent by L2
    /// (originating from another document's <see cref="DocumentIdentifierEntry.NormalizedValue"/>).
    /// Repository looks up <see cref="Contract.NormalizedContractNumber"/> — same normalization
    /// rule applied on the storage side, so cross-module matching just works.
    /// </summary>
    public virtual async Task<IReadOnlyList<Guid>> FindDocumentsAsync(
        string identifierType,
        string normalizedIdentifierValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedIdentifierValue))
        {
            return Array.Empty<Guid>();
        }

        return identifierType switch
        {
            ContractNumberTypeId => await FindByContractNumberAsync(normalizedIdentifierValue, cancellationToken),
            // PartyName intentionally absent — return-empty for any unsupported type
            // (codex review fix [high]).
            _ => Array.Empty<Guid>()
        };
    }

    protected virtual async Task<IReadOnlyList<Guid>> FindByContractNumberAsync(
        string normalizedContractNumber,
        CancellationToken ct)
    {
        var contracts = await _contractRepository.FindByContractNumberAsync(normalizedContractNumber, ct);
        return contracts.Select(c => c.DocumentId).Where(id => id != Guid.Empty).Distinct().ToList();
    }

    /// <summary>
    /// Helper: emit a (Type, RawValue, NormalizedValue) entry using the
    /// <see cref="DocumentIdentifierNormalization.NormalizeIdentifierCode"/> helper. Skipped
    /// when raw is whitespace OR when normalization yields an empty key (raw was punctuation-only).
    /// </summary>
    private static void AddIfPresent(List<DocumentIdentifierEntry> entries, string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var normalized = DocumentIdentifierNormalization.NormalizeIdentifierCode(value);
        if (string.IsNullOrEmpty(normalized)) return;
        entries.Add(new DocumentIdentifierEntry(type, value.Trim(), normalized));
    }
}
