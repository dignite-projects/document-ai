using Dignite.DocumentAI.Documents.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.DocumentAI;

[DependsOn(
    typeof(DocumentAIDomainModule),
    typeof(DocumentAITestBaseModule)
)]
public class DocumentAIDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Manager 依赖 IDocumentPipelineRunRepository（#216）；用 Domain.Tests 共享的 closure-state fake
        // 让 QueueAsync / DeriveLifecycle 的 DB 查询路径在内存里就能完整跑通。
        context.Services.AddSingleton(PipelineRunRepositoryFake.Create());
    }
}
