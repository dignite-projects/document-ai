using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents;
using Dignite.Paperbase.Documents.Pipelines;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace Dignite.Paperbase.EntityFrameworkCore.Documents;

public class DocumentPipelineRunExtraProperties_Tests
    : PaperbaseEntityFrameworkCoreTestBase
{
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IDocumentPipelineRunRepository _runRepository;
    private readonly DocumentPipelineRunManager _pipelineRunManager;
    private readonly IGuidGenerator _guidGenerator;

    public DocumentPipelineRunExtraProperties_Tests()
    {
        _documentRepository = GetRequiredService<IRepository<Document, Guid>>();
        _runRepository = GetRequiredService<IDocumentPipelineRunRepository>();
        _pipelineRunManager = GetRequiredService<DocumentPipelineRunManager>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task Should_RoundTrip_Classification_Candidates_In_ExtraProperties()
    {
        var documentId = _guidGenerator.Create();
        Guid runId = default;

        // #216：PipelineRun 拆为独立聚合根后 FK 强制 Document 先持久化才能 Insert run。
        // 分两个 UoW：先插 Document，再起 run。
        await WithUnitOfWorkAsync(async () =>
        {
            await _documentRepository.InsertAsync(CreateDocument(documentId), autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var document = await _documentRepository.GetAsync(documentId, includeDetails: false);
            var run = await _pipelineRunManager.StartAsync(document, PaperbasePipelines.Classification);
            runId = run.Id;

            run.SetProperty(
                PipelineRunExtraPropertyNames.ClassificationCandidates,
                new List<PipelineRunCandidate>
                {
                    new("contract.general", 0.64),
                    new("invoice.standard", 0.31)
                });
            // Manager.StartAsync 已 _runRepo.InsertAsync；UoW commit 时一并 flush 含 SetProperty 后的 ExtraProperties。
        });

        await WithUnitOfWorkAsync(async () =>
        {
            // 通过 runRepo 按 Id 加载（独立聚合根读路径）。
            var run = await _runRepository.FindAsync(runId);

            run.ShouldNotBeNull();

            var candidates = run.GetProperty(PipelineRunExtraPropertyNames.ClassificationCandidates);

            var json = candidates.ShouldBeOfType<JsonElement>();
            json.ValueKind.ShouldBe(JsonValueKind.Array);
            json.GetArrayLength().ShouldBe(2);

            var first = json[0];
            first.GetProperty(nameof(PipelineRunCandidate.TypeCode)).GetString().ShouldBe("contract.general");
            first.GetProperty(nameof(PipelineRunCandidate.ConfidenceScore)).GetDouble().ShouldBe(0.64);

            var second = json[1];
            second.GetProperty(nameof(PipelineRunCandidate.TypeCode)).GetString().ShouldBe("invoice.standard");
            second.GetProperty(nameof(PipelineRunCandidate.ConfidenceScore)).GetDouble().ShouldBe(0.31);
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
