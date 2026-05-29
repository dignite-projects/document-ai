using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace Dignite.Paperbase.Documents.Cabinets;

public class CreateCabinetDto
{
    [Required]
    [DynamicStringLength(typeof(CabinetConsts), nameof(CabinetConsts.MaxNameLength))]
    public string Name { get; set; } = default!;
}
