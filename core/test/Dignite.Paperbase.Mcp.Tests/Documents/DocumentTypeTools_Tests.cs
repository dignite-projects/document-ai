using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Paperbase.Ai;
using Dignite.Paperbase.Documents;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Paperbase.Mcp.Documents;

[DependsOn(typeof(PaperbaseTestBaseModule))]
public class DocumentTypeToolsTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton(Substitute.For<IDocumentTypeAppService>());
        context.Services.AddSingleton(Substitute.For<IFieldDefinitionAppService>());
    }
}

/// <summary>
/// <see cref="DocumentTypeTools.ListAsync"/> 薄壳行为：
/// 委托 <see cref="IDocumentTypeAppService.GetVisibleAsync"/> + <see cref="IFieldDefinitionAppService.GetListAsync"/>
/// 并把结果映射为 <see cref="DocumentTypeSchema"/>（displayName 经 <c>PromptBoundary</c> 包裹）。
/// </summary>
public class DocumentTypeTools_Tests : PaperbaseTestBase<DocumentTypeToolsTestModule>
{
    private readonly IDocumentTypeAppService _documentTypeAppService;
    private readonly IFieldDefinitionAppService _fieldDefinitionAppService;

    public DocumentTypeTools_Tests()
    {
        _documentTypeAppService = GetRequiredService<IDocumentTypeAppService>();
        _fieldDefinitionAppService = GetRequiredService<IFieldDefinitionAppService>();
    }

    [Fact]
    public async Task Returns_types_with_fields_and_wraps_display_names()
    {
        var typeId = Guid.NewGuid();
        _documentTypeAppService
            .GetVisibleAsync()
            .Returns(new List<DocumentTypeDto>
            {
                new()
                {
                    Id = typeId,
                    TypeCode = "contract.general",
                    DisplayName = "General Contract"
                }
            });
        _fieldDefinitionAppService
            .GetListAsync(Arg.Is<GetFieldDefinitionListInput>(i => i.DocumentTypeId == typeId))
            .Returns(new List<FieldDefinitionDto>
            {
                new()
                {
                    Name = "amount",
                    DataType = FieldDataType.Number,
                    AllowMultiple = false,
                    DisplayName = "Amount",
                    IsRequired = true,
                    DisplayOrder = 0
                },
                new()
                {
                    Name = "party_name",
                    DataType = FieldDataType.Text,
                    AllowMultiple = false,
                    DisplayName = "Party Name",
                    IsRequired = false,
                    DisplayOrder = 1
                }
            });

        var result = await DocumentTypeTools.ListAsync(
            _documentTypeAppService, _fieldDefinitionAppService);

        result.Count.ShouldBe(1);
        var schema = result[0];
        schema.TypeCode.ShouldBe("contract.general");
        // DisplayName 必须经 PromptBoundary 包裹。
        schema.DisplayName.ShouldBe(PromptBoundary.WrapField("General Contract"));
        schema.Fields.Count.ShouldBe(2);

        var amountField = schema.Fields[0];
        amountField.Name.ShouldBe("amount");
        amountField.DataType.ShouldBe("Number");
        amountField.AllowMultiple.ShouldBeFalse();
        amountField.IsRequired.ShouldBeTrue();
        amountField.DisplayName.ShouldBe(PromptBoundary.WrapField("Amount"));

        var partyField = schema.Fields[1];
        partyField.Name.ShouldBe("party_name");
        partyField.DataType.ShouldBe("Text");
        partyField.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public async Task Returns_empty_list_when_no_visible_types()
    {
        _documentTypeAppService.GetVisibleAsync().Returns(new List<DocumentTypeDto>());

        var result = await DocumentTypeTools.ListAsync(
            _documentTypeAppService, _fieldDefinitionAppService);

        result.ShouldBeEmpty();
        await _fieldDefinitionAppService.DidNotReceive().GetListAsync(Arg.Any<GetFieldDefinitionListInput>());
    }
}
