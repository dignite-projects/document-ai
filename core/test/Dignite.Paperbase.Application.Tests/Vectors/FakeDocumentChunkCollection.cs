using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.Vectors;
using Microsoft.Extensions.VectorData;

namespace Dignite.Paperbase.Tests.Vectors;

// In-memory fake of VectorStoreCollection<Guid, DocumentChunkRecord> used by all
// Application.Tests that previously NSubstituted IDocumentKnowledgeIndex. Stores
// records in a Dictionary; GetAsync evaluates LINQ filters server-side via
// expression compilation; SearchAsync returns pre-staged hits so tests don't have
// to wire real embeddings. No hybrid path — see DocumentTextSearchAdapter for why
// the production code keeps dense-only.
public class FakeDocumentChunkCollection
    : VectorStoreCollection<Guid, DocumentChunkRecord>
{
    private readonly Dictionary<Guid, DocumentChunkRecord> _store = new();
    private readonly List<List<DocumentChunkRecord>> _upsertBatches = new();
    private readonly List<List<Guid>> _deleteBatches = new();

    // Queue of result batches; each SearchAsync call drains the next batch.
    // Empty queue → empty result.
    public Queue<IReadOnlyList<VectorSearchResult<DocumentChunkRecord>>> StagedSearchResults { get; } = new();

    public IReadOnlyDictionary<Guid, DocumentChunkRecord> Store => _store;
    public IReadOnlyList<IReadOnlyList<DocumentChunkRecord>> UpsertBatches => _upsertBatches;
    public IReadOnlyList<IReadOnlyList<Guid>> DeleteBatches => _deleteBatches;

    public int UpsertCalls { get; private set; }
    public int DeleteCalls { get; private set; }
    public int SearchCalls { get; private set; }
    public int GetByFilterCalls { get; private set; }
    public int EnsureCollectionExistsCalls { get; private set; }

    public Func<DocumentChunkRecord, bool>? LastGetFilter { get; private set; }
    public Func<DocumentChunkRecord, bool>? LastSearchFilter { get; private set; }
    public int LastSearchTop { get; private set; }

    public bool ThrowOnSearch { get; set; }
    public bool ThrowOnUpsert { get; set; }
    public Exception SearchException { get; set; } = new InvalidOperationException("vector store down");

    public Action? OnUpsertInvoked { get; set; }
    public Action? OnDeleteInvoked { get; set; }
    public Action? OnGetByFilterInvoked { get; set; }

    public override string Name => "test-collection";

    public void Seed(params DocumentChunkRecord[] records)
    {
        foreach (var r in records)
        {
            _store[r.Id] = r;
        }
    }

    public void Reset()
    {
        _store.Clear();
        _upsertBatches.Clear();
        _deleteBatches.Clear();
        StagedSearchResults.Clear();
        UpsertCalls = 0;
        DeleteCalls = 0;
        SearchCalls = 0;
        GetByFilterCalls = 0;
        EnsureCollectionExistsCalls = 0;
        LastGetFilter = null;
        LastSearchFilter = null;
        LastSearchTop = 0;
        ThrowOnSearch = false;
        ThrowOnUpsert = false;
        OnUpsertInvoked = null;
        OnDeleteInvoked = null;
        OnGetByFilterInvoked = null;
    }

    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        EnsureCollectionExistsCalls++;
        return Task.CompletedTask;
    }

    public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        _store.Clear();
        return Task.CompletedTask;
    }

    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public override Task UpsertAsync(DocumentChunkRecord record, CancellationToken cancellationToken = default)
    {
        UpsertCalls++;
        OnUpsertInvoked?.Invoke();
        if (ThrowOnUpsert) throw SearchException;
        _store[record.Id] = record;
        _upsertBatches.Add(new List<DocumentChunkRecord> { record });
        return Task.CompletedTask;
    }

    public override Task UpsertAsync(IEnumerable<DocumentChunkRecord> records, CancellationToken cancellationToken = default)
    {
        UpsertCalls++;
        OnUpsertInvoked?.Invoke();
        if (ThrowOnUpsert) throw SearchException;
        var batch = records.ToList();
        foreach (var r in batch)
        {
            _store[r.Id] = r;
        }
        _upsertBatches.Add(batch);
        return Task.CompletedTask;
    }

    public override Task DeleteAsync(Guid key, CancellationToken cancellationToken = default)
    {
        DeleteCalls++;
        OnDeleteInvoked?.Invoke();
        _store.Remove(key);
        _deleteBatches.Add(new List<Guid> { key });
        return Task.CompletedTask;
    }

    public override Task DeleteAsync(IEnumerable<Guid> keys, CancellationToken cancellationToken = default)
    {
        DeleteCalls++;
        OnDeleteInvoked?.Invoke();
        var batch = keys.ToList();
        foreach (var k in batch)
        {
            _store.Remove(k);
        }
        _deleteBatches.Add(batch);
        return Task.CompletedTask;
    }

    public override Task<DocumentChunkRecord?> GetAsync(
        Guid key,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(key, out var rec);
        return Task.FromResult<DocumentChunkRecord?>(rec);
    }

    public override async IAsyncEnumerable<DocumentChunkRecord> GetAsync(
        Expression<Func<DocumentChunkRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<DocumentChunkRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        GetByFilterCalls++;
        OnGetByFilterInvoked?.Invoke();
        var compiled = filter.Compile();
        LastGetFilter = compiled;

        var hits = _store.Values.Where(compiled).Take(top).ToList();
        foreach (var hit in hits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return hit;
        }
    }

    public override async IAsyncEnumerable<VectorSearchResult<DocumentChunkRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<DocumentChunkRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        SearchCalls++;
        var filter = options?.Filter?.Compile();
        LastSearchFilter = filter;
        LastSearchTop = top;

        if (ThrowOnSearch)
        {
            throw SearchException;
        }

        var batch = StagedSearchResults.Count > 0
            ? StagedSearchResults.Dequeue()
            : Array.Empty<VectorSearchResult<DocumentChunkRecord>>();
        // Apply the same filter the production server would — keeps fake behavior
        // close to real Qdrant payload filtering so tests don't have to manually
        // strip rows that the production filter would have excluded.
        if (filter != null)
        {
            batch = batch.Where(r => r.Record != null && filter(r.Record)).ToList();
        }

        foreach (var hit in batch.Take(top))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return hit;
        }
    }

    public override object? GetService(Type serviceType, object? serviceKey = null) => null;
}
