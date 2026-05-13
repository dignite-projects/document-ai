using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.Contracts.Contracts;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace Dignite.Paperbase.Contracts.Chat;

/// <summary>
/// Issue #149: MAF agent skill answering arithmetic questions over the contract
/// store — count and total amount grouped by currency, optionally filtered by
/// party, signed date range, or status. Vector search over document chunks loses
/// arithmetic semantics; this skill is the only correct path for "how many active
/// contracts with party X" or "total signed amount in Q1".
///
/// <para>fail-closed safety: see <see cref="ContractSkillHelpers"/>.</para>
/// </summary>
[ExposeServices(typeof(AgentSkill))]
public sealed class AggregateContractsSkill : AgentClassSkill<AggregateContractsSkill>, ITransientDependency
{
    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "aggregate-contracts",
        "Aggregate contract counts and total amounts grouped by currency. Optional filters: party name, signed-date range, contract status. Use for arithmetic questions (\"how many active contracts with X\", \"total signed amount in Q1\") that vector search over document chunks cannot answer.");

    protected override string Instructions => """
        Use this skill when the user asks an arithmetic question over contracts —
        counts, sums, totals grouped by currency. Vector search over document
        chunks loses arithmetic semantics, so questions like "how many active
        contracts with party X" or "total signed amount in Q1" can only be answered
        here.

        Steps:
        1. Identify the filter criteria (party name, signed date range, status) from
           the user's question. Each parameter on the `invoke` script is optional.
        2. Call the `invoke` script.
        3. The result contains `buckets` (one per currency) with `count` and
           `totalAmount`, plus a `grandCount`. Present the figures clearly. If the
           question is metadata-only and the structured answer fully resolves it,
           STOP — do not also call vector search; that is wasted cost and risks
           contradicting the structured result.

        Result fields are numeric / enum identifiers only — no user-derived free text
        is returned, so no `<field>` wrapping is needed.
        """;

    // IServiceProvider is reordered ahead of the user-facing filters so the C# compiler
    // accepts `= null` defaults on every optional filter. Without the defaults, the
    // common "totals for party X" call (only one filter supplied) would fail
    // JSON-schema validation — caught by Codex adversarial review (Issue #149).
    [AgentSkillScript("invoke")]
    [Description("Aggregate contract counts and totals grouped by currency. All filter parameters are optional.")]
    private static async Task<string> InvokeAsync(
        IServiceProvider serviceProvider,
        [Description("Optional party name filter — matches Party A, Party B, or counterparty (partial match).")]
        string? partyName = null,
        [Description("Earliest signed date in ISO 8601 format, e.g. 2024-01-01.")]
        DateTime? signedDateFrom = null,
        [Description("Latest signed date in ISO 8601 format.")]
        DateTime? signedDateTo = null,
        [Description("Optional contract status filter: Draft, Active, Expired, Terminated, or Archived.")]
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();
        await ContractSkillHelpers.RequireContractsReadAsync(authorizationService);

        var repository = serviceProvider.GetRequiredService<IContractRepository>();
        var currentTenant = serviceProvider.GetRequiredService<ICurrentTenant>();
        var executer = serviceProvider.GetRequiredService<IAsyncQueryableExecuter>();

        var queryable = await ContractSkillHelpers.ScopedQueryableAsync(repository, currentTenant);
        queryable = queryable.Where(c => !c.NeedsReview);

        if (!string.IsNullOrWhiteSpace(partyName))
            queryable = queryable.Where(c =>
                (c.PartyAName != null && c.PartyAName.Contains(partyName)) ||
                (c.PartyBName != null && c.PartyBName.Contains(partyName)) ||
                (c.CounterpartyName != null && c.CounterpartyName.Contains(partyName)));

        if (signedDateFrom.HasValue)
            queryable = queryable.Where(c => c.SignedDate >= signedDateFrom);
        if (signedDateTo.HasValue)
            queryable = queryable.Where(c => c.SignedDate <= signedDateTo);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ContractStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            queryable = queryable.Where(c => c.Status == parsedStatus);
        }

        var grouped = queryable
            .GroupBy(c => c.Currency)
            .Select(g => new
            {
                currency = g.Key,
                count = g.Count(),
                totalAmount = g.Sum(c => c.TotalAmount ?? 0m)
            });

        var buckets = await executer.ToListAsync(grouped, cancellationToken);

        var result = new
        {
            groupBy = "currency",
            buckets,
            grandCount = buckets.Sum(b => b.count)
        };

        return JsonSerializer.Serialize(result);
    }
}
