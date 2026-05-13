using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents.Events;
using Dignite.Paperbase.Tests.Vectors;
using Dignite.Paperbase.Vectors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Paperbase.Vectors;

public class DocumentDeletingEventHandler_Tests
{
    private readonly IUnitOfWorkManager _uowManager;
    private readonly IUnitOfWork _ambientUow;
    private readonly FakeDocumentChunkCollection _collection;
    private readonly FakeDocumentChunkCollectionProvider _provider;
    private readonly ILogger<DocumentDeletingEventHandler> _logger;
    private readonly DocumentDeletingEventHandler _handler;

    public DocumentDeletingEventHandler_Tests()
    {
        _ambientUow = Substitute.For<IUnitOfWork>();
        _collection = new FakeDocumentChunkCollection();
        _provider = new FakeDocumentChunkCollectionProvider(_collection);
        _logger = Substitute.For<ILogger<DocumentDeletingEventHandler>>();

        _uowManager = Substitute.For<IUnitOfWorkManager>();
        _uowManager.Current.Returns(_ambientUow);

        _handler = new DocumentDeletingEventHandler(
            _uowManager,
            _provider,
            Options.Create(new PaperbaseVectorStoreOptions()),
            _logger);
    }

    [Fact]
    public async Task HandleEventAsync_Registers_OnCompleted_Callback()
    {
        var evt = new DocumentDeletingEvent(Guid.NewGuid(), Guid.NewGuid());

        await _handler.HandleEventAsync(evt);

        _ambientUow.Received(1).OnCompleted(Arg.Any<Func<Task>>());
    }

    [Fact]
    public async Task HandleEventAsync_Does_Not_Delete_Index_Immediately()
    {
        var evt = new DocumentDeletingEvent(Guid.NewGuid(), Guid.NewGuid());

        await _handler.HandleEventAsync(evt);

        _collection.DeleteCalls.ShouldBe(0);
    }

    [Fact]
    public async Task OnCompleted_Callback_Deletes_Document_Index()
    {
        var documentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var evt = new DocumentDeletingEvent(documentId, tenantId);

        var staleId = DocumentChunkPointId.Create(tenantId, documentId, 0);
        _collection.Seed(new DocumentChunkRecord
        {
            Id = staleId,
            TenantId = DocumentChunkPayloadEncoding.EncodeTenantId(tenantId),
            DocumentId = DocumentChunkPayloadEncoding.EncodeDocumentId(documentId),
            ChunkIndex = 0,
            Text = "stale",
        });

        Func<Task>? capturedCallback = null;
        _ambientUow
            .When(u => u.OnCompleted(Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.Arg<Func<Task>>());

        await _handler.HandleEventAsync(evt);

        capturedCallback.ShouldNotBeNull();
        await capturedCallback!();

        _collection.DeleteCalls.ShouldBe(1);
        _collection.DeleteBatches[0].ShouldContain(staleId);
        _collection.Store.ShouldNotContainKey(staleId);
    }

    [Fact]
    public async Task HandleEventAsync_DoesNothing_When_No_AmbientUoW()
    {
        _uowManager.Current.Returns((IUnitOfWork?)null);

        await _handler.HandleEventAsync(new DocumentDeletingEvent(Guid.NewGuid(), null));

        _ambientUow.DidNotReceive().OnCompleted(Arg.Any<Func<Task>>());
        _collection.DeleteCalls.ShouldBe(0);
        _logger.ReceivedWithAnyArgs(1).Log(
            LogLevel.Warning,
            default,
            default!,
            default,
            default!);
    }

    [Fact]
    public async Task OnCompleted_Callback_Swallows_Index_Delete_Failure()
    {
        var documentId = Guid.NewGuid();
        var evt = new DocumentDeletingEvent(documentId, null);

        // Replace the collection in the provider with one that throws on GetAsync —
        // verifies the handler's try/catch swallows the failure into a logger.Error
        // rather than crashing the OnCompleted callback.
        var throwingCollection = new ThrowingDocumentChunkCollection();
        var throwingProvider = new FakeDocumentChunkCollectionProvider(throwingCollection);
        var throwingHandler = new DocumentDeletingEventHandler(
            _uowManager,
            throwingProvider,
            Options.Create(new PaperbaseVectorStoreOptions()),
            _logger);

        Func<Task>? capturedCallback = null;
        _ambientUow
            .When(u => u.OnCompleted(Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.Arg<Func<Task>>());

        await throwingHandler.HandleEventAsync(evt);

        capturedCallback.ShouldNotBeNull();
        await capturedCallback!();

        _logger.ReceivedWithAnyArgs(1).Log(
            LogLevel.Error,
            default,
            default!,
            default,
            default!);
    }

    [Fact]
    public async Task TenantIsolation_Cascade_Delete_Does_Not_Touch_Other_Tenant()
    {
        // End-to-end guard: seed tenant A and tenant B chunks under the SAME
        // document id. Cascade delete fires for tenant A. The handler's filter
        // must scope to (tenant_id == A, document_id == docId); tenant B's
        // chunk under the same docId must survive.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var docKey = DocumentChunkPayloadEncoding.EncodeDocumentId(documentId);

        var idA = DocumentChunkPointId.Create(tenantA, documentId, 0);
        var idB = DocumentChunkPointId.Create(tenantB, documentId, 0);
        _collection.Seed(
            new DocumentChunkRecord
            {
                Id = idA,
                TenantId = DocumentChunkPayloadEncoding.EncodeTenantId(tenantA),
                DocumentId = docKey,
                ChunkIndex = 0,
                Text = "A-chunk",
            },
            new DocumentChunkRecord
            {
                Id = idB,
                TenantId = DocumentChunkPayloadEncoding.EncodeTenantId(tenantB),
                DocumentId = docKey,
                ChunkIndex = 0,
                Text = "B-chunk",
            });

        Func<Task>? capturedCallback = null;
        _ambientUow
            .When(u => u.OnCompleted(Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.Arg<Func<Task>>());

        await _handler.HandleEventAsync(new DocumentDeletingEvent(documentId, tenantA));
        await capturedCallback!();

        _collection.Store.ShouldNotContainKey(idA);
        _collection.Store.ShouldContainKey(idB);
    }

    [Fact]
    public async Task OnCompleted_Callback_Paginates_Through_All_Chunks_When_Total_Exceeds_PageSize()
    {
        // PR-8 guard: cleanup must page through (filter + DeleteAsync) until the
        // GetAsync iteration returns empty. With CleanupPageSize tuned small for the
        // test, a single document with > pageSize stragglers exercises the loop.
        const int pageSize = 4;
        const int totalChunks = 11; // 3 full pages + 1 leftover
        var documentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenantKey = DocumentChunkPayloadEncoding.EncodeTenantId(tenantId);
        var docKey = DocumentChunkPayloadEncoding.EncodeDocumentId(documentId);

        for (var i = 0; i < totalChunks; i++)
        {
            _collection.Seed(new DocumentChunkRecord
            {
                Id = DocumentChunkPointId.Create(tenantId, documentId, i),
                TenantId = tenantKey,
                DocumentId = docKey,
                ChunkIndex = i,
                Text = $"chunk-{i}",
            });
        }

        var pagedOptions = Options.Create(new PaperbaseVectorStoreOptions
        {
            CleanupPageSize = pageSize,
            CleanupMaxIterations = 10,
        });
        var pagedHandler = new DocumentDeletingEventHandler(
            _uowManager, _provider, pagedOptions, _logger);

        Func<Task>? capturedCallback = null;
        _ambientUow
            .When(u => u.OnCompleted(Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.Arg<Func<Task>>());

        await pagedHandler.HandleEventAsync(new DocumentDeletingEvent(documentId, tenantId));
        await capturedCallback!();

        // All chunks for this (tenant, document) gone after cleanup.
        _collection.Store.Values
            .Where(r => r.TenantId == tenantKey && r.DocumentId == docKey)
            .ShouldBeEmpty();
        // ceil(11/4) = 3 delete batches; GetAsync runs one extra time to see the
        // empty page that ends the loop.
        _collection.DeleteBatches.Count.ShouldBe(3);
        _collection.GetByFilterCalls.ShouldBe(4);
        // No warning — the loop converged inside the iteration cap.
        _logger.DidNotReceiveWithAnyArgs().Log(
            LogLevel.Warning, default, default!, default, default!);
    }

    [Fact]
    public async Task OnCompleted_Callback_Bails_When_Iteration_Cap_Hit_And_Logs_Warning()
    {
        // Pathological case: DeleteAsync silently no-ops (simulates eventual-
        // consistency lag where deleted records keep coming back from GetAsync).
        // The iteration cap must end the loop and emit a warning.
        const int pageSize = 2;
        const int maxIterations = 3;
        var documentId = Guid.NewGuid();
        var docKey = DocumentChunkPayloadEncoding.EncodeDocumentId(documentId);

        var stuckCollection = new StuckDocumentChunkCollection();
        for (var i = 0; i < pageSize; i++)
        {
            stuckCollection.Seed(new DocumentChunkRecord
            {
                Id = DocumentChunkPointId.Create(null, documentId, i),
                TenantId = DocumentChunkPayloadEncoding.HostTenantId,
                DocumentId = docKey,
                ChunkIndex = i,
                Text = $"stuck-{i}",
            });
        }
        var stuckProvider = new FakeDocumentChunkCollectionProvider(stuckCollection);

        var cappedOptions = Options.Create(new PaperbaseVectorStoreOptions
        {
            CleanupPageSize = pageSize,
            CleanupMaxIterations = maxIterations,
        });
        var cappedHandler = new DocumentDeletingEventHandler(
            _uowManager, stuckProvider, cappedOptions, _logger);

        Func<Task>? capturedCallback = null;
        _ambientUow
            .When(u => u.OnCompleted(Arg.Any<Func<Task>>()))
            .Do(ci => capturedCallback = ci.Arg<Func<Task>>());

        await cappedHandler.HandleEventAsync(new DocumentDeletingEvent(documentId, null));
        await capturedCallback!();

        stuckCollection.GetByFilterCalls.ShouldBe(maxIterations);
        _logger.ReceivedWithAnyArgs(1).Log(
            LogLevel.Warning, default, default!, default, default!);
    }

    private sealed class ThrowingDocumentChunkCollection : FakeDocumentChunkCollection
    {
#pragma warning disable CS1998
        public override async System.Collections.Generic.IAsyncEnumerable<DocumentChunkRecord> GetAsync(
            System.Linq.Expressions.Expression<Func<DocumentChunkRecord, bool>> filter,
            int top,
            Microsoft.Extensions.VectorData.FilteredRecordRetrievalOptions<DocumentChunkRecord>? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("qdrant unavailable");
            yield break;
        }
#pragma warning restore CS1998
    }

    private sealed class StuckDocumentChunkCollection : FakeDocumentChunkCollection
    {
        // Override DeleteAsync to be a no-op. Records stay in the store, so the
        // cascade-delete loop keeps seeing them on every GetAsync iteration —
        // exercises the CleanupMaxIterations safety cap.
        public override Task DeleteAsync(IEnumerable<Guid> keys, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
