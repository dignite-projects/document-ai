using Dignite.DocumentAI.Abstractions;
using Dignite.DocumentAI.Ocr;
using Volo.Abp.Modularity;

namespace Dignite.DocumentAI.TextExtraction;

[DependsOn(typeof(DocumentAIAbstractionsModule), typeof(DocumentAIOcrModule))]
public class DocumentAITextExtractionModule : AbpModule
{
}
