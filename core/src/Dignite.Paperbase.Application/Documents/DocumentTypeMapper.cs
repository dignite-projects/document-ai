using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Dignite.Paperbase.Documents;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DocumentTypeToDtoMapper : MapperBase<DocumentType, DocumentTypeDto>
{
    public override partial DocumentTypeDto Map(DocumentType source);
    public override partial void Map(DocumentType source, DocumentTypeDto destination);
}
