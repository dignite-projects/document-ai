using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.DocumentAI.Documents.Fields;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.DocumentAI.HttpApi.Documents.Fields;

[Area("document-ai")]
[Route("api/document-ai/field-definitions")]
public class FieldDefinitionController : DocumentAIController, IFieldDefinitionAppService
{
    private readonly IFieldDefinitionAppService _fieldDefinitionAppService;

    public FieldDefinitionController(IFieldDefinitionAppService fieldDefinitionAppService)
    {
        _fieldDefinitionAppService = fieldDefinitionAppService;
    }

    [HttpGet]
    public virtual Task<List<FieldDefinitionDto>> GetListAsync([FromQuery] GetFieldDefinitionListInput input)
    {
        return _fieldDefinitionAppService.GetListAsync(input);
    }

    [HttpPost]
    public virtual Task<FieldDefinitionDto> CreateAsync([FromBody] CreateFieldDefinitionDto input)
    {
        return _fieldDefinitionAppService.CreateAsync(input);
    }

    [HttpPut("{id}")]
    public virtual Task<FieldDefinitionDto> UpdateAsync(Guid id, [FromBody] UpdateFieldDefinitionDto input)
    {
        return _fieldDefinitionAppService.UpdateAsync(id, input);
    }

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _fieldDefinitionAppService.DeleteAsync(id);
    }

    [HttpPost("{id}/restore")]
    public virtual Task<FieldDefinitionDto> RestoreAsync(Guid id)
    {
        return _fieldDefinitionAppService.RestoreAsync(id);
    }
}
