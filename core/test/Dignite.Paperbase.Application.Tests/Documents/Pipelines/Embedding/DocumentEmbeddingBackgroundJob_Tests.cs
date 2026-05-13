using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase.Ai;
using Dignite.Paperbase.Documents;
using Dignite.Paperbase.Documents.Pipelines.Embedding;
using Dignite.Paperbase.Tests.Vectors;
using Dignite.Paperbase.Vectors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Paperbase.Documents;

[DependsOn(typeof(PaperbaseApplicationTestModule))]
public class DocumentEmbeddingJobTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton(Substitute.For<IDocumentRepository>());

        var fakeCollection = new FakeDocumentChunkCollection();
        context.Services.AddSingleton(fakeCollection);
        context.Services.AddSingleton<DocumentChunkCollectionProvider>(
            new FakeDocumentChunkCollectionProvider(fakeCollection));

        // TextChunker is a real DI-resolved service; replace the workflow itself with a mock
        // so we can return any chunk shape we want without depending on chunker / embedder behavior.
        var workflow = Substitute.ForPartsOf<DocumentEmbeddingWorkflow>(
            new TextChunker(Options.Create(new PaperbaseAIBehaviorOptions())),
            Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>());
        context.Services.AddSingleton(workflow);
    }
}

/// <summary>
/// DocumentEmbeddingBackgroundJob 行为测试：守护 PR-4 Slice — 写入路径切换到
/// MEVD <see cref="Microsoft.Extensions.VectorData.VectorStoreCollection{TKey, TRecord}"/>
/// 上 UpsertAsync + GetAsync(filter) + DeleteAsync(keys) 三段语义。重点关注：
///   - UpsertAsync 接收完整 DocumentChunkRecord（TenantId / DocumentId / DocumentTypeCode / Embedding）
///   - TenantId 来自 Document 显式拷贝，不依赖 ABP ambient ICurrentTenant
///   - 空 chunks 时仍执行 list+delete 清理路径（不需要 upsert）
/// </summary>
public class DocumentEmbeddingBackgroundJob_Tests
    : PaperbaseApplicationTestBase<DocumentEmbeddingJobTestModule>
{
    private readonly DocumentEmbeddingBackgroundJob _job;
    private readonly IDocumentRepository _documentRepository;
    private readonly FakeDocumentChunkCollection _collection;
    private readonly DocumentEmbeddingWorkflow _workflow;
    private readonly DocumentPipelineRunManager _pipelineRunManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public DocumentEmbeddingBackgroundJob_Tests()
    {
        _job = GetRequiredService<DocumentEmbeddingBackgroundJob>();
        _documentRepository = GetRequiredService<IDocumentRepository>();
        _collection = GetRequiredService<FakeDocumentChunkCollection>();
        _workflow = GetRequiredService<DocumentEmbeddingWorkflow>();
        _pipelineRunManager = GetRequiredService<DocumentPipelineRunManager>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
        _collection.Reset();
    }

    [Fact]
    public async Task Empty_Markdown_Skips_Job_Without_Touching_KnowledgeIndex()
    {
        // Markdown 为 null/whitespace 时整个 job 应静默退出，不创建 PipelineRun，
        // 也不能调用向量存储——避免对一个还没有内容的文档清空索引。
        var doc = CreateDocument(extractedText: null);
        SetupDocumentRepository(doc);

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = doc.Id });

        _collection.UpsertCalls.ShouldBe(0);
        _collection.DeleteCalls.ShouldBe(0);
        _collection.GetByFilterCalls.ShouldBe(0);

        doc.GetLatestRun(PaperbasePipelines.Embedding).ShouldBeNull();
    }

    [Fact]
    public async Task Job_Calls_UpsertAsync_With_Chunks()
    {
        // PR-4: the new path is UpsertAsync(records) then GetAsync(filter) + DeleteAsync(stragglers).
        // job からは1回ずつ呼ぶだけでよい。
        var doc = CreateDocument(extractedText: "契約書本文。");
        SetupDocumentRepository(doc);
        SetupWorkflowChunks([
            new DocumentEmbeddingChunk { ChunkIndex = 0, ChunkText = "chunk-0", Vector = MakeVector(0.1f) }
        ]);

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = doc.Id });

        _collection.UpsertCalls.ShouldBe(1);
        var batch = _collection.UpsertBatches.Single();
        batch.Count.ShouldBe(1);
        batch[0].DocumentId.ShouldBe(DocumentChunkPayloadEncoding.EncodeDocumentId(doc.Id));
        batch[0].TenantId.ShouldBe(DocumentChunkPayloadEncoding.EncodeTenantId(doc.TenantId));
    }

    [Fact]
    public async Task Job_Does_Not_Hold_UnitOfWork_While_Running_External_Work()
    {
        var doc = CreateDocument(extractedText: "契約書本文。");
        SetupDocumentRepository(doc);

        var chunks = new[]
        {
            new DocumentEmbeddingChunk { ChunkIndex = 0, ChunkText = "chunk-0", Vector = MakeVector(0.1f) }
        };
        _workflow
            .RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _unitOfWorkManager.Current.ShouldBeNull();
                return chunks;
            });
        _collection.OnUpsertInvoked = () => _unitOfWorkManager.Current.ShouldBeNull();
        _collection.OnGetByFilterInvoked = () => _unitOfWorkManager.Current.ShouldBeNull();

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = doc.Id });

        _collection.UpsertCalls.ShouldBe(1);
        _collection.GetByFilterCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Job_Uses_Precreated_Pending_Run_When_PipelineRunId_Is_Provided()
    {
        var doc = CreateDocument(extractedText: "契約書本文。");
        var pendingRun = await _pipelineRunManager.QueueAsync(doc, PaperbasePipelines.Embedding);
        SetupDocumentRepository(doc);
        SetupWorkflowChunks([
            new DocumentEmbeddingChunk { ChunkIndex = 0, ChunkText = "chunk-0", Vector = MakeVector(0.1f) }
        ]);

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs
        {
            DocumentId = doc.Id,
            PipelineRunId = pendingRun.Id
        });

        pendingRun.Status.ShouldBe(PipelineRunStatus.Succeeded);
        pendingRun.AttemptNumber.ShouldBe(1);
        doc.PipelineRuns.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Job_Maps_Workflow_Output_To_VectorRecord_With_Document_Context()
    {
        // DocumentChunkRecord の TenantId / DocumentId / DocumentTypeCode は Document から
        // 明示的にコピーされ、ambient context に依存しない（Hangfire job 安全性）。
        var tenantId = Guid.NewGuid();
        var doc = CreateDocument(
            extractedText: "業務委託契約書。",
            tenantId: tenantId,
            documentTypeCode: "contract.general");
        SetupDocumentRepository(doc);

        var chunk0 = new DocumentEmbeddingChunk { ChunkIndex = 0, ChunkText = "chunk-0", Vector = MakeVector(0.1f) };
        var chunk1 = new DocumentEmbeddingChunk { ChunkIndex = 1, ChunkText = "chunk-1", Vector = MakeVector(0.2f) };
        SetupWorkflowChunks([chunk0, chunk1]);

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = doc.Id });

        _collection.UpsertCalls.ShouldBe(1);
        var batch = _collection.UpsertBatches.Single();
        batch.Count.ShouldBe(2);

        var expectedTenantKey = DocumentChunkPayloadEncoding.EncodeTenantId(tenantId);
        var expectedDocKey = DocumentChunkPayloadEncoding.EncodeDocumentId(doc.Id);
        var rec0 = batch[0];
        rec0.TenantId.ShouldBe(expectedTenantKey);
        rec0.DocumentId.ShouldBe(expectedDocKey);
        rec0.DocumentTypeCode.ShouldBe("contract.general");
        rec0.ChunkIndex.ShouldBe(0);
        rec0.Text.ShouldBe("chunk-0");
        rec0.Embedding.Span[0].ShouldBe(0.1f);

        var rec1 = batch[1];
        rec1.ChunkIndex.ShouldBe(1);
        rec1.Text.ShouldBe("chunk-1");
        rec1.Embedding.Span[0].ShouldBe(0.2f);

        // Deterministic key derivation: same (tenant, doc, chunk) tuple → same Guid.
        rec0.Id.ShouldBe(DocumentChunkPointId.Create(tenantId, doc.Id, 0));
        rec1.Id.ShouldBe(DocumentChunkPointId.Create(tenantId, doc.Id, 1));
    }

    [Fact]
    public async Task Job_Performs_Stale_Cleanup_When_Workflow_Returns_None()
    {
        // 分块器が0件を返した場合、Upsert は呼ばれない（書く対象がない）が、
        // 既存ストラグラー削除のために GetAsync(filter) + DeleteAsync は走る。
        var tenantId = Guid.NewGuid();
        var doc = CreateDocument(extractedText: "短文本", tenantId: tenantId);
        SetupDocumentRepository(doc);

        // Seed a stale chunk under (tenant, document) — the job's cleanup pass must remove it.
        var staleId = DocumentChunkPointId.Create(tenantId, doc.Id, 0);
        _collection.Seed(new DocumentChunkRecord
        {
            Id = staleId,
            TenantId = DocumentChunkPayloadEncoding.EncodeTenantId(tenantId),
            DocumentId = DocumentChunkPayloadEncoding.EncodeDocumentId(doc.Id),
            ChunkIndex = 0,
            Text = "stale",
        });

        SetupWorkflowChunks([]);

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = doc.Id });

        _collection.UpsertCalls.ShouldBe(0);
        _collection.GetByFilterCalls.ShouldBe(1);
        _collection.DeleteBatches.Single().ShouldContain(staleId);

        var run = doc.GetLatestRun(PaperbasePipelines.Embedding);
        run.ShouldNotBeNull();
        run.Status.ShouldBe(PipelineRunStatus.Succeeded);
    }

    [Fact]
    public async Task TenantIsolation_Stale_Cleanup_Does_Not_Touch_Other_Tenant()
    {
        // End-to-end guard: seed two tenants' chunks under the SAME document id
        // (degenerate but valid — Document.Id Guid does not enforce per-tenant
        // uniqueness in this fake). Re-embed under tenant A with 0 chunks. The
        // cleanup filter must scope to (tenant_id == A, document_id == docId);
        // tenant B's chunk under the same docId must survive.
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var doc = CreateDocument(extractedText: "irrelevant", tenantId: tenantA);
        SetupDocumentRepository(doc);

        var docKey = DocumentChunkPayloadEncoding.EncodeDocumentId(doc.Id);
        var staleIdA = DocumentChunkPointId.Create(tenantA, doc.Id, 0);
        var survivorIdB = DocumentChunkPointId.Create(tenantB, doc.Id, 0);

        _collection.Seed(
            new DocumentChunkRecord
            {
                Id = staleIdA,
                TenantId = DocumentChunkPayloadEncoding.EncodeTenantId(tenantA),
                DocumentId = docKey,
                ChunkIndex = 0,
                Text = "A-stale",
            },
            new DocumentChunkRecord
            {
                Id = survivorIdB,
                TenantId = DocumentChunkPayloadEncoding.EncodeTenantId(tenantB),
                DocumentId = docKey,
                ChunkIndex = 0,
                Text = "B-survivor",
            });

        SetupWorkflowChunks([]);

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = doc.Id });

        _collection.Store.ShouldNotContainKey(staleIdA);
        _collection.Store.ShouldContainKey(survivorIdB);
    }

    [Fact]
    public async Task Job_Paginates_Stale_Cleanup_When_Total_Exceeds_PageSize()
    {
        // PR-8 guard: stale cleanup must page through (GetAsync + DeleteAsync) until
        // the page returns within bound. The dynamic page size is keysToKeep.Count +
        // CleanupPageSize, so with 0 keepers it reduces to CleanupPageSize — we tune
        // both small for the test and seed enough stragglers to force multiple loops.
        const int pageSize = 3;
        const int staleCount = 10;
        var options = GetRequiredService<IOptions<PaperbaseVectorStoreOptions>>().Value;
        var originalPage = options.CleanupPageSize;
        var originalCap = options.CleanupMaxIterations;
        options.CleanupPageSize = pageSize;
        options.CleanupMaxIterations = 10;

        try
        {
            var tenantId = Guid.NewGuid();
            var doc = CreateDocument(extractedText: "stale-many", tenantId: tenantId);
            SetupDocumentRepository(doc);

            var tenantKey = DocumentChunkPayloadEncoding.EncodeTenantId(tenantId);
            var docKey = DocumentChunkPayloadEncoding.EncodeDocumentId(doc.Id);
            for (var i = 0; i < staleCount; i++)
            {
                _collection.Seed(new DocumentChunkRecord
                {
                    Id = DocumentChunkPointId.Create(tenantId, doc.Id, i),
                    TenantId = tenantKey,
                    DocumentId = docKey,
                    ChunkIndex = i,
                    Text = $"stale-{i}",
                });
            }

            SetupWorkflowChunks([]);

            await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = doc.Id });

            // All stragglers removed.
            _collection.Store.Values
                .Where(r => r.TenantId == tenantKey && r.DocumentId == docKey)
                .ShouldBeEmpty();
            // Loop ran multiple times — at least ceil(10/3) = 4 GetAsync calls
            // (the last one sees a partial page and exits).
            _collection.GetByFilterCalls.ShouldBeGreaterThanOrEqualTo(3);
            _collection.DeleteBatches.Count.ShouldBeGreaterThanOrEqualTo(3);
            _collection.DeleteBatches.Sum(b => b.Count).ShouldBe(staleCount);
        }
        finally
        {
            options.CleanupPageSize = originalPage;
            options.CleanupMaxIterations = originalCap;
        }
    }

    [Fact]
    public async Task KnowledgeIndex_Failure_Marks_Run_Failed_Without_Rethrowing()
    {
        var doc = CreateDocument(extractedText: "契約書本文。");
        SetupDocumentRepository(doc);
        SetupWorkflowChunks([
            new DocumentEmbeddingChunk { ChunkIndex = 0, ChunkText = "chunk-0", Vector = MakeVector(0.1f) }
        ]);

        _collection.ThrowOnUpsert = true;
        _collection.SearchException = new InvalidOperationException("vector store down");

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = doc.Id });

        var run = doc.GetLatestRun(PaperbasePipelines.Embedding);
        run.ShouldNotBeNull();
        run.Status.ShouldBe(PipelineRunStatus.Failed);
        run.StatusMessage.ShouldBe("vector store down");
    }

    [Fact]
    public async Task Different_Tenants_Get_Different_TenantId_In_Update()
    {
        // 多租户隔離：连续两份文档的 UpsertAsync 收到的 DocumentChunkRecord.TenantId
        // 必须严格匹配各自的 Document.TenantId。
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var docA = CreateDocument(extractedText: "A", tenantId: tenantA);
        var docB = CreateDocument(extractedText: "B", tenantId: tenantB);
        _documentRepository.GetAsync(docA.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(docA);
        _documentRepository.GetAsync(docB.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(docB);

        SetupWorkflowChunks([
            new DocumentEmbeddingChunk { ChunkIndex = 0, ChunkText = "x", Vector = MakeVector(0.1f) }
        ]);

        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = docA.Id });
        await _job.ExecuteAsync(new DocumentEmbeddingJobArgs { DocumentId = docB.Id });

        _collection.UpsertCalls.ShouldBe(2);
        _collection.UpsertBatches[0].Single().TenantId
            .ShouldBe(DocumentChunkPayloadEncoding.EncodeTenantId(tenantA));
        _collection.UpsertBatches[1].Single().TenantId
            .ShouldBe(DocumentChunkPayloadEncoding.EncodeTenantId(tenantB));
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private void SetupDocumentRepository(Document doc)
    {
        _documentRepository
            .GetAsync(doc.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(doc);
    }

    private void SetupWorkflowChunks(IReadOnlyList<DocumentEmbeddingChunk> chunks)
    {
        _workflow
            .RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chunks);
    }

    private static float[] MakeVector(float firstValue)
    {
        // Reflects PaperbaseVectorStoreOptions default value, not runtime config.
        var v = new float[new PaperbaseVectorStoreOptions().EmbeddingDimension];
        v[0] = firstValue;
        return v;
    }

    private static Document CreateDocument(
        string? extractedText,
        Guid? tenantId = null,
        string? documentTypeCode = null)
    {
        var doc = new Document(
            Guid.NewGuid(), tenantId,
            $"blobs/{Guid.NewGuid():N}.pdf",
            SourceType.Digital,
            new FileOrigin(
                uploadedByUserName: "test-user",
                contentType: "application/pdf",
                contentHash: $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
                fileSize: 1024,
                originalFileName: "test.pdf"));

        if (extractedText != null)
        {
            typeof(Document)
                .GetProperty(nameof(Document.Markdown))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(doc, [extractedText]);
        }

        if (documentTypeCode != null)
        {
            typeof(Document)
                .GetProperty(nameof(Document.DocumentTypeCode))!
                .GetSetMethod(nonPublic: true)!
                .Invoke(doc, [documentTypeCode]);
        }

        return doc;
    }
}
