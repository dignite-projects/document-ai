using Dignite.DocumentAI.Abstractions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.DocumentAI;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpBlobStoringModule),
    typeof(DocumentAIAbstractionsModule),
    typeof(DocumentAIDomainSharedModule)
)]
public class DocumentAIDomainModule : AbpModule
{
}
