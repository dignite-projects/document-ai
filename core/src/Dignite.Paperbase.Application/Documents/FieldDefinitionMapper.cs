using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Dignite.Paperbase.Documents;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FieldDefinitionToDtoMapper : MapperBase<FieldDefinition, FieldDefinitionDto>
{
    public override partial FieldDefinitionDto Map(FieldDefinition source);
    public override partial void Map(FieldDefinition source, FieldDefinitionDto destination);
}
