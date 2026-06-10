using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Paperbase.Ai;
using Dignite.Paperbase.Documents;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Paperbase.Mcp.Documents;

[DependsOn(typeof(PaperbaseTestBaseModule))]
public class DocumentToolsTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton(Substitute.For<IDocumentAppService>());
    }
}

/// <summary>
/// <see cref="DocumentTools.GetAsync"/> 薄壳行为：委托 <see cref="IDocumentAppService.GetAsync"/>
/// 并映射为 <see cref="DocumentDetailResult"/>（title / markdown 经 <c>PromptBoundary</c> 包裹）。
/// </summary>
public class DocumentTools_Tests : PaperbaseTestBase<DocumentToolsTestModule>
{
    private readonly IDocumentAppService _documentAppService;

    public DocumentTools_Tests()
    {
        _documentAppService = GetRequiredService<IDocumentAppService>();
    }

    [Fact]
    public async Task Returns_document_with_wrapped_title_and_markdown()
    {
        var docId = Guid.NewGuid();
        _documentAppService
            .GetAsync(docId)
            .Returns(new DocumentDto
            {
                Id = docId,
                Title = "Acme MSA 2025",
                DocumentTypeCode = "contract.general",
                LifecycleStatus = DocumentLifecycleStatus.Ready,
                Language = "zh",
                CreationTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Markdown = "## Acme MSA\n\nContract text here.",
                ExtractionIsComplete = true,
                ExtractedFields = new Dictionary<string, JsonElement>
                {
                    ["amount"] = JsonSerializer.SerializeToElement(100000)
                }
            });

        var result = await DocumentTools.GetAsync(docId.ToString(), _documentAppService);

        result.Id.ShouldBe(docId);
        // Title 必须经 PromptBoundary.WrapField 包裹。
        result.Title.ShouldBe(PromptBoundary.WrapField("Acme MSA 2025"));
        result.DocumentTypeCode.ShouldBe("contract.general");
        result.LifecycleStatus.ShouldBe("Ready");
        result.Language.ShouldBe("zh");
        // Markdown 必须经 PromptBoundary.WrapDocument 包裹。
        result.Markdown.ShouldBe(PromptBoundary.WrapDocument("## Acme MSA\n\nContract text here."));
        result.ExtractionIsComplete.ShouldBeTrue();
        result.ExtractedFields.ShouldNotBeNull();
        // 数字字段值原样透传（非 String，不包裹）。
        result.ExtractedFields!["amount"].GetInt32().ShouldBe(100000);
    }

    [Fact]
    public async Task Wraps_string_extracted_fields()
    {
        var docId = Guid.NewGuid();
        _documentAppService
            .GetAsync(docId)
            .Returns(new DocumentDto
            {
                Id = docId,
                Title = "Test",
                LifecycleStatus = DocumentLifecycleStatus.Ready,
                Markdown = "",
                ExtractionIsComplete = true,
                ExtractedFields = new Dictionary<string, JsonElement>
                {
                    ["party_name"] = JsonSerializer.SerializeToElement("Acme Corp")
                }
            });

        var result = await DocumentTools.GetAsync(docId.ToString(), _documentAppService);

        // 文本字段值（用户派生自由文本）必须经 PromptBoundary.WrapField 包裹。
        result.ExtractedFields!["party_name"].GetString()
            .ShouldBe(PromptBoundary.WrapField("Acme Corp"));
    }

    [Fact]
    public async Task Throws_on_invalid_id_format()
    {
        await Should.ThrowAsync<McpException>(async () =>
            await DocumentTools.GetAsync("not-a-guid", _documentAppService));
    }

    [Fact]
    public async Task Throws_not_found_when_entity_missing()
    {
        var docId = Guid.NewGuid();
        _documentAppService
            .GetAsync(docId)
            .Throws(new EntityNotFoundException());

        var ex = await Should.ThrowAsync<McpException>(async () =>
            await DocumentTools.GetAsync(docId.ToString(), _documentAppService));

        ex.Message.ShouldContain(docId.ToString());
    }

    [Fact]
    public async Task Exposes_extraction_incomplete_reason()
    {
        var docId = Guid.NewGuid();
        _documentAppService
            .GetAsync(docId)
            .Returns(new DocumentDto
            {
                Id = docId,
                Title = "Incomplete",
                LifecycleStatus = DocumentLifecycleStatus.Ready,
                Markdown = "",
                ExtractionIsComplete = false,
                ExtractionIncompleteReason = "Content truncated by VLM guard"
            });

        var result = await DocumentTools.GetAsync(docId.ToString(), _documentAppService);

        result.ExtractionIsComplete.ShouldBeFalse();
        result.ExtractionIncompleteReason.ShouldBe("Content truncated by VLM guard");
    }
}
