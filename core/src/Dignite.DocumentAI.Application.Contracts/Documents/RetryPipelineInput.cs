using System.ComponentModel.DataAnnotations;

namespace Dignite.DocumentAI.Documents;

public class RetryPipelineInput
{
    [Required]
    [StringLength(64)]
    public string PipelineCode { get; set; } = default!;
}
