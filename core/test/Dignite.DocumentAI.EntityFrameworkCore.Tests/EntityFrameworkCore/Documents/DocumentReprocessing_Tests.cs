using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.DocumentAI.Documents;
using Dignite.DocumentAI.Documents.DocumentTypes;
using Dignite.DocumentAI.Documents.Pipelines;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace Dignite.DocumentAI.EntityFrameworkCore.Documents;

/// <summary>
/// 批量重处理（#289）仓储范围查询（<see cref="IDocumentRepository.CountForReprocessingAsync"/> /
/// <see cref="IDocumentRepository.GetIdsForReprocessingAsync"/>）+ <c>field-extraction</c> pipeline 生命周期中性的集成测试。
/// </summary>
public class DocumentReprocessing_Tests : DocumentAIEntityFrameworkCoreTestBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly DocumentPipelineRunManager _pipelineRunManager;
    private readonly IGuidGenerator _guidGenerator;

    // 稳定 Id 让断言可读（GetIds 按 Id 升序，断言用集合比较不依赖具体顺序）。
    private static readonly Guid TypeAId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TypeBId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    public DocumentReprocessing_Tests()
    {
        _documentRepository = GetRequiredService<IDocumentRepository>();
        _documentTypeRepository = GetRequiredService<IDocumentTypeRepository>();
        _pipelineRunManager = GetRequiredService<DocumentPipelineRunManager>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task Scope_By_Type_Excludes_NeverExtracted_And_SoftDeleted()
    {
        var ids = await SeedAsync();

        var count = await WithUnitOfWorkAsync(() =>
            _documentRepository.CountForReprocessingAsync(TypeAId, withReason: null, excludeManuallyConfirmed: false));
        // typeA 有文本: d1(auto) + d2(reviewed)；d4 无 markdown、d6 软删 —— 均排除。
        count.ShouldBe(2);

        var pageIds = await WithUnitOfWorkAsync(() =>
            _documentRepository.GetIdsForReprocessingAsync(TypeAId, null, false, afterId: null, maxCount: 100));
        pageIds.ShouldBe(new[] { ids.D1, ids.D2 }, ignoreOrder: true);
    }

    [Fact]
    public async Task Exclude_Manually_Confirmed_Drops_Reviewed_Documents()
    {
        var ids = await SeedAsync();

        var count = await WithUnitOfWorkAsync(() =>
            _documentRepository.CountForReprocessingAsync(TypeAId, withReason: null, excludeManuallyConfirmed: true));
        // 保护人工确认：d2(Reviewed) 被排除，只剩 d1。
        count.ShouldBe(1);

        var pageIds = await WithUnitOfWorkAsync(() =>
            _documentRepository.GetIdsForReprocessingAsync(TypeAId, null, true, null, 100));
        pageIds.ShouldBe(new[] { ids.D1 });
    }

    [Fact]
    public async Task PendingReview_Scope_Returns_Only_PendingReview_Documents()
    {
        var ids = await SeedAsync();

        var count = await WithUnitOfWorkAsync(() =>
            _documentRepository.CountForReprocessingAsync(
                documentTypeId: null, withReason: DocumentReviewReasons.UnresolvedClassification, excludeManuallyConfirmed: false));
        count.ShouldBe(1);

        var pageIds = await WithUnitOfWorkAsync(() =>
            _documentRepository.GetIdsForReprocessingAsync(null, DocumentReviewReasons.UnresolvedClassification, false, null, 100));
        pageIds.ShouldBe(new[] { ids.D5 });
    }

    [Fact]
    public async Task AllDocuments_Scope_Counts_Every_TextExtracted_Active_Document()
    {
        await SeedAsync();

        var count = await WithUnitOfWorkAsync(() =>
            _documentRepository.CountForReprocessingAsync(null, null, false));
        // d1,d2,d3,d5 有文本且活跃；d4 无 markdown、d6 软删除排除。
        count.ShouldBe(4);
    }

    [Fact]
    public async Task Keyset_Pagination_Returns_Every_Id_Once_Across_Batches()
    {
        await SeedAsync();

        var collected = new List<Guid>();
        Guid? cursor = null;
        // batchSize=1 强制多批，验证链式游标不重不漏。
        for (var guard = 0; guard < 50; guard++)
        {
            var batch = await WithUnitOfWorkAsync(() =>
                _documentRepository.GetIdsForReprocessingAsync(null, null, false, cursor, maxCount: 1));
            if (batch.Count == 0)
            {
                break;
            }

            collected.AddRange(batch);
            cursor = batch[^1];
        }

        collected.Count.ShouldBe(4);
        collected.Distinct().Count().ShouldBe(4); // 无重复
    }

    [Fact]
    public async Task FieldExtraction_Run_Is_Lifecycle_Neutral_On_Ready_Document()
    {
        var documentId = _guidGenerator.Create();

        // 构造 Ready 文档：text-extraction + classification 两条 key pipeline 均 Succeeded + 有类型。
        await WithUnitOfWorkAsync(async () =>
        {
            await EnsureTypeAsync(TypeAId, "type.a");
            var doc = NewDocument(documentId);
            doc.SetMarkdown("# Body");
            doc.ApplyAutomaticClassificationResult(TypeAId, 0.99);
            await _documentRepository.InsertAsync(doc, autoSave: true);

            var te = await _pipelineRunManager.StartAsync(doc, DocumentAIPipelines.TextExtraction);
            await _pipelineRunManager.CompleteAsync(doc, te);
            var cls = await _pipelineRunManager.StartAsync(doc, DocumentAIPipelines.Classification);
            await _pipelineRunManager.CompleteAsync(doc, cls);
            await _documentRepository.UpdateAsync(doc, autoSave: true);
        });

        (await ReloadLifecycleAsync(documentId)).ShouldBe(DocumentLifecycleStatus.Ready);

        // 跑一个完整的 field-extraction run：Pending → Running → Succeeded，断言生命周期全程保持 Ready。
        await WithUnitOfWorkAsync(async () =>
        {
            var doc = await _documentRepository.GetAsync(documentId);
            var fe = await _pipelineRunManager.StartAsync(doc, DocumentAIPipelines.FieldExtraction);
            // StartAsync 内已 Queue(Pending)→Begin(Running) 并各派生一次 lifecycle。
            doc.LifecycleStatus.ShouldBe(DocumentLifecycleStatus.Ready);
            await _pipelineRunManager.CompleteAsync(doc, fe);
            doc.LifecycleStatus.ShouldBe(DocumentLifecycleStatus.Ready);
            await _documentRepository.UpdateAsync(doc, autoSave: true);
        });

        (await ReloadLifecycleAsync(documentId)).ShouldBe(DocumentLifecycleStatus.Ready);
    }

    // ─── helpers ───────────────────────────────────────────────────────────

    private sealed record SeededIds(Guid D1, Guid D2, Guid D3, Guid D4, Guid D5, Guid D6);

    private async Task<SeededIds> SeedAsync()
    {
        var ids = new SeededIds(
            _guidGenerator.Create(), _guidGenerator.Create(), _guidGenerator.Create(),
            _guidGenerator.Create(), _guidGenerator.Create(), _guidGenerator.Create());

        await WithUnitOfWorkAsync(async () =>
        {
            await EnsureTypeAsync(TypeAId, "type.a");
            await EnsureTypeAsync(TypeBId, "type.b");

            // d1: typeA, 有文本, 自动分类(None)
            await InsertAsync(ids.D1, markdown: "# d1", d => d.ApplyAutomaticClassificationResult(TypeAId, 0.9));
            // d2: typeA, 有文本, 人工确认(Reviewed)
            await InsertAsync(ids.D2, markdown: "# d2", d => d.ConfirmClassification(TypeAId));
            // d3: typeB, 有文本, 自动分类(None)
            await InsertAsync(ids.D3, markdown: "# d3", d => d.ApplyAutomaticClassificationResult(TypeBId, 0.8));
            // d4: typeA, 无 markdown（never-extracted）—— 应被排除
            await InsertAsync(ids.D4, markdown: null, d => d.ApplyAutomaticClassificationResult(TypeAId, 0.9));
            // d5: 无类型, 有文本, 待审核(PendingReview)
            await InsertAsync(ids.D5, markdown: "# d5", d => d.RequestClassificationReview());
            // d6: typeA, 有文本, 自动分类, 软删除 —— 应被排除
            await InsertAsync(ids.D6, markdown: "# d6", d => d.ApplyAutomaticClassificationResult(TypeAId, 0.9));
            await _documentRepository.DeleteAsync(ids.D6); // soft delete
        });

        return ids;
    }

    private async Task InsertAsync(Guid id, string? markdown, Action<Document> mutate)
    {
        var doc = NewDocument(id);
        if (markdown != null)
        {
            doc.SetMarkdown(markdown);
        }
        mutate(doc);
        await _documentRepository.InsertAsync(doc, autoSave: true);
    }

    private async Task EnsureTypeAsync(Guid id, string code)
    {
        if (await _documentTypeRepository.FindAsync(id) == null)
        {
            await _documentTypeRepository.InsertAsync(
                new DocumentType(id, tenantId: null, typeCode: code, displayName: code), autoSave: true);
        }
    }

    private async Task<DocumentLifecycleStatus> ReloadLifecycleAsync(Guid id)
    {
        return await WithUnitOfWorkAsync(async () =>
            (await _documentRepository.GetAsync(id)).LifecycleStatus);
    }

    private static Document NewDocument(Guid id) =>
        new(
            id,
            tenantId: null,
            fileOrigin: new FileOrigin(
                blobName: $"blobs/{id:N}.pdf",
                uploadedByUserName: "test-user",
                contentType: "application/pdf",
                contentHash: $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
                fileSize: 1024,
                originalFileName: "test.pdf"));
}
