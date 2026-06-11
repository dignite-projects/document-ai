using System.Collections.Generic;
using Dignite.DocumentAI.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.DocumentAI.Ai;

/// <summary>
/// Test module that stacks an in-memory <c>DocumentAIBehavior</c> configuration
/// section on top of whatever <see cref="DocumentAIApplicationTestModule"/> already
/// provides. <see cref="DocumentAIApplicationModule.ConfigureServices"/> binds
/// <see cref="DocumentAIBehaviorOptions"/> to that section, so this module is the
/// vehicle that lets the test prove the binding is wired end-to-end.
/// </summary>
[DependsOn(typeof(DocumentAIApplicationTestModule))]
public class DocumentAIBehaviorOptionsBindingTestModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var existing = context.Services.GetConfiguration();
        var stacked = new ConfigurationBuilder()
            .AddConfiguration(existing)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentAIBehavior:DefaultLanguage"] = "en",
                ["DocumentAIBehavior:MaxDocumentTypesInClassificationPrompt"] = "25",
                ["DocumentAIBehavior:MaxTextLengthPerExtraction"] = "16000",
                ["DocumentAIBehavior:MaxTitleGenerationMarkdownLength"] = "2048",
            })
            .Build();

        context.Services.ReplaceConfiguration(stacked);
    }
}

/// <summary>
/// Acceptance test: configuration values placed under the <c>DocumentAIBehavior</c>
/// JSON section must reach <see cref="DocumentAIBehaviorOptions"/> consumers via
/// <see cref="IOptions{T}"/>.
/// </summary>
public class DocumentAIBehaviorOptionsBinding_Tests
    : DocumentAIApplicationTestBase<DocumentAIBehaviorOptionsBindingTestModule>
{
    private readonly DocumentAIBehaviorOptions _options;

    public DocumentAIBehaviorOptionsBinding_Tests()
    {
        _options = GetRequiredService<IOptions<DocumentAIBehaviorOptions>>().Value;
    }

    [Fact]
    public void Configuration_Values_Flow_Through_To_Options()
    {
        // Each assertion would fail with the class default if the
        // DocumentAIBehavior → DocumentAIBehaviorOptions binding ever regresses.
        _options.DefaultLanguage.ShouldBe("en");                                      // default "ja"
        _options.MaxDocumentTypesInClassificationPrompt.ShouldBe(25);                 // default 50
        _options.MaxTextLengthPerExtraction.ShouldBe(16000);                          // default 8000
        _options.MaxTitleGenerationMarkdownLength.ShouldBe(2048);                     // default 4000
    }
}
