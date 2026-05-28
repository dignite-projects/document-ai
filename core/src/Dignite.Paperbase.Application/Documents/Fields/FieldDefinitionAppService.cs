using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Paperbase.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Dignite.Paperbase.Documents.Fields;

[Authorize(PaperbasePermissions.Documents.ConfirmClassification)]
public class FieldDefinitionAppService : PaperbaseAppService, IFieldDefinitionAppService
{
    private readonly IFieldDefinitionRepository _repository;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly IDocumentRepository _documentRepository;

    public FieldDefinitionAppService(
        IFieldDefinitionRepository repository,
        IDocumentTypeRepository documentTypeRepository,
        IDocumentRepository documentRepository)
    {
        _repository = repository;
        _documentTypeRepository = documentTypeRepository;
        _documentRepository = documentRepository;
    }

    public virtual async Task<List<FieldDefinitionDto>> GetByDocumentTypeAsync(string documentTypeCode)
    {
        // 仅当前租户层字段（CLAUDE.md "两层 mutually exclusive 不混"）。解析外部 TypeCode → 内部 DocumentTypeId（#207）。
        var type = await _documentTypeRepository.FindByTypeCodeAsync(documentTypeCode);
        if (type == null)
        {
            return new List<FieldDefinitionDto>();
        }

        var list = await _repository.GetByDocumentTypeAsync(type.Id);
        return MapToDtos(list, documentTypeCode);
    }

    public virtual async Task<List<FieldDefinitionDto>> GetDeletedByDocumentTypeAsync(string documentTypeCode)
    {
        // 当前层回收站：Host admin（CurrentTenant.Id IS NULL）看 Host 字段；租户 admin 看自己租户。
        var type = await _documentTypeRepository.FindByTypeCodeAsync(documentTypeCode);
        if (type == null)
        {
            return new List<FieldDefinitionDto>();
        }

        using (DataFilter.Disable<ISoftDelete>())
        {
            var queryable = await _repository.GetQueryableAsync();
            var list = await AsyncExecuter.ToListAsync(
                queryable
                    .Where(f =>
                        f.TenantId == CurrentTenant.Id &&
                        f.DocumentTypeId == type.Id &&
                        f.IsDeleted)
                    .OrderByDescending(f => f.DeletionTime));
            return MapToDtos(list, documentTypeCode);
        }
    }

    public virtual async Task<FieldDefinitionDto> CreateAsync(CreateFieldDefinitionDto input)
    {
        // 严格单层创建：Host admin 创建 TenantId IS NULL 字段；租户 admin 创建自己租户字段。
        // 解析父类型（#207 必须存在——FieldDefinition.DocumentTypeId FK RESTRICT）。
        var type = await _documentTypeRepository.FindByTypeCodeAsync(input.DocumentTypeCode);
        if (type == null)
        {
            throw new BusinessException(PaperbaseErrorCodes.InvalidDocumentTypeCode)
                .WithData(nameof(input.DocumentTypeCode), input.DocumentTypeCode);
        }

        // 关闭 ISoftDelete 过滤——同 (TenantId, DocumentTypeId, Name) 即使软删除态也算占用，避免恢复时与新记录冲突。
        FieldDefinition? existing;
        using (DataFilter.Disable<ISoftDelete>())
        {
            existing = await _repository.FindByNameAsync(type.Id, input.Name);
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
            type.Id,
            input.Name,
            input.DisplayName,
            input.Prompt,
            input.DataType,
            input.DisplayOrder,
            input.IsRequired);

        await _repository.InsertAsync(entity, autoSave: true);
        return MapToDto(entity, input.DocumentTypeCode);
    }

    public virtual async Task<FieldDefinitionDto> UpdateAsync(Guid id, UpdateFieldDefinitionDto input)
    {
        var entity = await _repository.GetAsync(id);

        // 跨层防御：只能改自己所在层（Host admin 改 TenantId IS NULL；租户 admin 改 TenantId == 自己）。
        if (entity.TenantId != CurrentTenant.Id)
        {
            throw new EntityNotFoundException(typeof(FieldDefinition), id);
        }

        var documentTypeCode = await ResolveTypeCodeAsync(entity.DocumentTypeId);

        // 重命名解锁（#207）：仅在 Name 变化时判重（同层同类型唯一，含软删占用）。
        if (!string.Equals(input.Name, entity.Name, StringComparison.Ordinal))
        {
            FieldDefinition? conflict;
            using (DataFilter.Disable<ISoftDelete>())
            {
                conflict = await _repository.FindByNameAsync(entity.DocumentTypeId, input.Name);
            }
            if (conflict != null)
            {
                throw new BusinessException(PaperbaseErrorCodes.FieldDefinitionAlreadyExists)
                    .WithData("DocumentTypeCode", documentTypeCode ?? string.Empty)
                    .WithData("Name", input.Name);
            }
        }

        // DataType 变更守卫（#207）：已有抽取值的字段禁止改 DataType——历史值落在旧 typed 列，按新类型查会静默漏掉。
        // 需换类型请新建字段。
        if (input.DataType != entity.DataType
            && await _documentRepository.AnyExtractedFieldValueAsync(entity.Id))
        {
            throw new BusinessException(PaperbaseErrorCodes.FieldDefinitionDataTypeChangeNotAllowed)
                .WithData("Name", entity.Name);
        }

        entity.Update(input.Name, input.DisplayName, input.Prompt, input.DataType, input.DisplayOrder, input.IsRequired);
        await _repository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity, documentTypeCode);
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

            // 已在 Disable<ISoftDelete> 作用域内——可解析到（即便已软删的）父类型 TypeCode 用于错误信息 / DTO。
            var parentType = await _documentTypeRepository.FindAsync(entity.DocumentTypeId);
            var documentTypeCode = parentType?.TypeCode;

            // 幂等：未删除直接返回。
            if (!entity.IsDeleted)
            {
                return MapToDto(entity, documentTypeCode);
            }

            // 父类型必须存在且活跃——严格单层匹配（与 FieldExtractionEventHandler 一致）。
            // 父类型仍处于已删除态时，应走 IDocumentTypeAppService.RestoreAsync 的级联路径。
            if (parentType == null || parentType.IsDeleted)
            {
                throw new BusinessException(PaperbaseErrorCodes.FieldDefinitionParentTypeMissing)
                    .WithData("DocumentTypeCode", documentTypeCode ?? string.Empty)
                    .WithData("Name", entity.Name);
            }

            // 同名活跃字段冲突——CreateAsync 判重应当已防住，防御性补一道。
            var queryable = await _repository.GetQueryableAsync();
            var nameConflict = await AsyncExecuter.AnyAsync(
                queryable.Where(f =>
                    f.TenantId == entity.TenantId &&
                    f.DocumentTypeId == entity.DocumentTypeId &&
                    f.Name == entity.Name &&
                    !f.IsDeleted));
            if (nameConflict)
            {
                throw new BusinessException(PaperbaseErrorCodes.FieldDefinitionRestoreConflict)
                    .WithData("DocumentTypeCode", documentTypeCode ?? string.Empty)
                    .WithData("Name", entity.Name);
            }

            entity.IsDeleted = false;
            entity.DeletionTime = null;
            entity.DeleterId = null;
            await _repository.UpdateAsync(entity);

            return MapToDto(entity, documentTypeCode);
        }
    }

    /// <summary>解析字段所属类型的 TypeCode（穿透 soft-delete），用于回填 DTO 的 DocumentTypeCode（外部 wire-format，#207）。</summary>
    protected virtual async Task<string?> ResolveTypeCodeAsync(Guid documentTypeId)
    {
        using (DataFilter.Disable<ISoftDelete>())
        {
            var type = await _documentTypeRepository.FindAsync(documentTypeId);
            return type?.TypeCode;
        }
    }

    private FieldDefinitionDto MapToDto(FieldDefinition entity, string? documentTypeCode)
    {
        var dto = ObjectMapper.Map<FieldDefinition, FieldDefinitionDto>(entity);
        dto.DocumentTypeCode = documentTypeCode!;
        return dto;
    }

    private List<FieldDefinitionDto> MapToDtos(List<FieldDefinition> entities, string documentTypeCode)
    {
        var dtos = ObjectMapper.Map<List<FieldDefinition>, List<FieldDefinitionDto>>(entities);
        foreach (var dto in dtos)
        {
            dto.DocumentTypeCode = documentTypeCode;
        }
        return dtos;
    }
}
