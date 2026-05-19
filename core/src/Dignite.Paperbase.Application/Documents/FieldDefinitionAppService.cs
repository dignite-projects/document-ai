using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Paperbase.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Dignite.Paperbase.Documents;

[Authorize(PaperbasePermissions.Documents.ConfirmClassification)]
public class FieldDefinitionAppService : PaperbaseAppService, IFieldDefinitionAppService
{
    private readonly IFieldDefinitionRepository _repository;
    private readonly IDocumentTypeRepository _documentTypeRepository;

    public FieldDefinitionAppService(
        IFieldDefinitionRepository repository,
        IDocumentTypeRepository documentTypeRepository)
    {
        _repository = repository;
        _documentTypeRepository = documentTypeRepository;
    }

    public virtual async Task<List<FieldDefinitionDto>> GetByDocumentTypeAsync(string documentTypeCode)
    {
        // 返回当前租户视图：Host 字段 + 当前租户字段
        var list = await _repository.GetByDocumentTypeAsync(CurrentTenant.Id, documentTypeCode);
        return ObjectMapper.Map<List<FieldDefinition>, List<FieldDefinitionDto>>(list);
    }

    public virtual async Task<List<FieldDefinitionDto>> GetDeletedByDocumentTypeAsync(string documentTypeCode)
    {
        // 仅当前租户层的软删除字段；Host 字段不参与租户级回收站。
        using (DataFilter.Disable<ISoftDelete>())
        {
            var queryable = await _repository.GetQueryableAsync();
            var list = await AsyncExecuter.ToListAsync(
                queryable
                    .Where(f =>
                        f.TenantId == CurrentTenant.Id &&
                        f.DocumentTypeCode == documentTypeCode &&
                        f.IsDeleted)
                    .OrderByDescending(f => f.DeletionTime));
            return ObjectMapper.Map<List<FieldDefinition>, List<FieldDefinitionDto>>(list);
        }
    }

    public virtual async Task<FieldDefinitionDto> CreateAsync(CreateFieldDefinitionDto input)
    {
        // 仅创建当前租户私有字段；Host 字段（TenantId IS NULL）通过 IDataSeedContributor 维护。
        // 关闭 ISoftDelete 过滤——同 (TenantId, DocumentTypeCode, Name) 即使处于软删除态也算占用，
        // 避免后续恢复时与新记录冲突。
        FieldDefinition? existing;
        using (DataFilter.Disable<ISoftDelete>())
        {
            existing = await _repository.FindByNameAsync(CurrentTenant.Id, input.DocumentTypeCode, input.Name);
        }
        if (existing != null)
        {
            throw new BusinessException(PaperbaseErrorCodes.FieldDefinitionAlreadyExists)
                .WithData("DocumentTypeCode", input.DocumentTypeCode)
                .WithData("Name", input.Name);
        }

        var entity = new FieldDefinition(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.DocumentTypeCode,
            input.Name,
            input.Prompt,
            input.DataType,
            input.DisplayOrder,
            input.IsRequired);

        await _repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<FieldDefinition, FieldDefinitionDto>(entity);
    }

    public virtual async Task<FieldDefinitionDto> UpdateAsync(Guid id, UpdateFieldDefinitionDto input)
    {
        var entity = await _repository.GetAsync(id);

        // 跨租户防御 + 禁止改 Host 字段（TenantId IS NULL）
        if (entity.TenantId != CurrentTenant.Id)
        {
            throw new EntityNotFoundException(typeof(FieldDefinition), id);
        }

        entity.Update(input.Prompt, input.DataType, input.DisplayOrder, input.IsRequired);
        await _repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<FieldDefinition, FieldDefinitionDto>(entity);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.TenantId != CurrentTenant.Id)
        {
            throw new EntityNotFoundException(typeof(FieldDefinition), id);
        }
        await _repository.DeleteAsync(entity);
    }

    public virtual async Task<FieldDefinitionDto> RestoreAsync(Guid id)
    {
        using (DataFilter.Disable<ISoftDelete>())
        {
            var entity = await _repository.GetAsync(id);
            if (entity.TenantId != CurrentTenant.Id)
            {
                throw new EntityNotFoundException(typeof(FieldDefinition), id);
            }

            // 幂等：未删除直接返回。
            if (!entity.IsDeleted)
            {
                return ObjectMapper.Map<FieldDefinition, FieldDefinitionDto>(entity);
            }

            // 父类型必须存在且活跃——否则恢复字段语义错位（孤儿字段无意义）。
            // 父类型仍处于已删除态时，应走 IDocumentTypeAppService.RestoreAsync 的级联路径。
            var parent = await _documentTypeRepository.FindByTypeCodeAsync(
                CurrentTenant.Id, entity.DocumentTypeCode);
            if (parent == null || parent.IsDeleted)
            {
                throw new BusinessException(PaperbaseErrorCodes.FieldDefinitionParentTypeMissing)
                    .WithData("DocumentTypeCode", entity.DocumentTypeCode)
                    .WithData("Name", entity.Name);
            }

            // 同名活跃字段冲突——CreateAsync 判重应当已防住，防御性补一道。
            var queryable = await _repository.GetQueryableAsync();
            var nameConflict = await AsyncExecuter.AnyAsync(
                queryable.Where(f =>
                    f.TenantId == entity.TenantId &&
                    f.DocumentTypeCode == entity.DocumentTypeCode &&
                    f.Name == entity.Name &&
                    !f.IsDeleted));
            if (nameConflict)
            {
                throw new BusinessException(PaperbaseErrorCodes.FieldDefinitionRestoreConflict)
                    .WithData("DocumentTypeCode", entity.DocumentTypeCode)
                    .WithData("Name", entity.Name);
            }

            entity.IsDeleted = false;
            entity.DeletionTime = null;
            entity.DeleterId = null;
            await _repository.UpdateAsync(entity);

            return ObjectMapper.Map<FieldDefinition, FieldDefinitionDto>(entity);
        }
    }
}
