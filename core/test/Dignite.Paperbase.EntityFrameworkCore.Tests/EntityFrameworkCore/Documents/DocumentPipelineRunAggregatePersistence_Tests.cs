using System;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents;
using Dignite.Paperbase.Documents.Pipelines;
using Shouldly;
using Volo.Abp.Guids;
using Xunit;

namespace Dignite.Paperbase.EntityFrameworkCore.Documents;

public class DocumentPipelineRunAggregatePersistence_Tests
    : PaperbaseEntityFrameworkCoreTestBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentPipelineRunRepository _runRepository;
    private readonly DocumentPipelineRunManager _pipelineRunManager;
    private readonly IGuidGenerator _guidGenerator;

    public DocumentPipelineRunAggregatePersistence_Tests()
    {
        _documentRepository = GetRequiredService<IDocumentRepository>();
        _runRepository = GetRequiredService<IDocumentPipelineRunRepository>();
        _pipelineRunManager = GetRequiredService<DocumentPipelineRunManager>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task Independent_Aggregate_Persists_New_Run_For_Document()
    {
        var documentId = _guidGenerator.Create();
        Guid runId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            await _documentRepository.InsertAsync(CreateDocument(documentId), autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            // #216：PipelineRun 拆为独立聚合根后 GetAsync 不再 eager-load runs；Manager.QueueAsync 经 runRepo InsertAsync。
            var document = await _documentRepository.GetAsync(documentId, includeDetails: false);
            var run = await _pipelineRunManager.QueueAsync(document, PaperbasePipelines.Classification);
            runId = run.Id;

            await _documentRepository.UpdateAsync(document, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            // 通过 runRepo 直接查（不再经 Document 聚合根）——验证独立聚合根持久化路径生效。
            var persistedRun = await _runRepository.FindAsync(runId);
            persistedRun.ShouldNotBeNull();
            persistedRun.DocumentId.ShouldBe(documentId);
            persistedRun.PipelineCode.ShouldBe(PaperbasePipelines.Classification);
            persistedRun.Status.ShouldBe(PipelineRunStatus.Pending);
        });
    }

    private static Document CreateDocument(Guid id)
    {
        return new Document(
            id,
            tenantId: null,
            fileOrigin: new FileOrigin(
                blobName: "blobs/test.pdf",
                uploadedByUserName: "test-user",
                contentType: "application/pdf",
                contentHash: $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
                fileSize: 1024,
                originalFileName: "test.pdf"));
    }
}
