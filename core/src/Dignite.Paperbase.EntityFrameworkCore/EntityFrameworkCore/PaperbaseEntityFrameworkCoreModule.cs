using Dignite.Paperbase.Documents;
using Dignite.Paperbase.Documents.Exports;
using Dignite.Paperbase.Documents.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.Paperbase.EntityFrameworkCore;

[DependsOn(
    typeof(PaperbaseDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class PaperbaseEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<PaperbaseDbContext>(options =>
        {
            options.AddDefaultRepositories();

            options.AddRepository<Document, EfCoreDocumentRepository>();
            options.AddRepository<DocumentType, EfCoreDocumentTypeRepository>();
            options.AddRepository<FieldDefinition, EfCoreFieldDefinitionRepository>();
            options.AddRepository<Cabinet, EfCoreCabinetRepository>();
            options.AddRepository<ExportTemplate, EfCoreExportTemplateRepository>();
            // #216：PipelineRun 升级独立聚合根
            options.AddRepository<DocumentPipelineRun, EfCoreDocumentPipelineRunRepository>();
        });
    }
}
