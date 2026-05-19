using System.Linq;
using System.Threading.Tasks;
using Dignite.Paperbase.Abstractions.Documents;
using Dignite.Paperbase.Documents;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Dignite.Paperbase.Host.Data;

/// <summary>
/// 字段架构 v2：Host 部署级文档类型 + 字段定义的种子化。
/// <para>
/// 把 <see cref="DocumentTypeOptions"/>（Host 在 <c>ConfigureServices</c> 中声明的 schema）
/// 翻译成 DB 行（<see cref="DocumentType"/> + <see cref="FieldDefinition"/>）的 <c>TenantId IS NULL</c>
/// 记录。Host 部署者改 <c>Configure&lt;DocumentTypeOptions&gt;(...)</c> 后下一次启动 upsert 生效。
/// </para>
/// <para>
/// Upsert 策略（idempotent）：
/// <list type="bullet">
///   <item>每次启动按 Options.Types 逐项 find-or-create + update 元数据（DisplayName / ConfidenceThreshold / Priority）</item>
///   <item>类型下的 Host 字段（<see cref="DocumentTypeDefinition.Fields"/>）同样 upsert</item>
///   <item>Options.Types 中已不存在但 DB 中仍有 <c>TenantId IS NULL</c> 行的类型 / 字段不主动 delete，
///         保留人工审计窗口；如要清理走显式 admin path</item>
/// </list>
/// </para>
/// </summary>
public class HostDocumentTypeDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly DocumentTypeOptions _documentTypeOptions;
    private readonly IDocumentTypeRepository _documentTypeRepository;
    private readonly IFieldDefinitionRepository _fieldDefinitionRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStringLocalizerFactory _stringLocalizerFactory;

    public HostDocumentTypeDataSeedContributor(
        IOptions<DocumentTypeOptions> documentTypeOptions,
        IDocumentTypeRepository documentTypeRepository,
        IFieldDefinitionRepository fieldDefinitionRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IStringLocalizerFactory stringLocalizerFactory)
    {
        _documentTypeOptions = documentTypeOptions.Value;
        _documentTypeRepository = documentTypeRepository;
        _fieldDefinitionRepository = fieldDefinitionRepository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _stringLocalizerFactory = stringLocalizerFactory;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Host 级种子：显式切到 TenantId = null 上下文
        using (_currentTenant.Change(null))
        {
            foreach (var typeDef in _documentTypeOptions.Types)
            {
                // DisplayName 在种子化时按默认 culture 解析 ILocalizableString 后存为 string
                var resolvedDisplayName = typeDef.DisplayName.Localize(_stringLocalizerFactory).Value;

                var existingType = await _documentTypeRepository.FindByTypeCodeAsync(null, typeDef.TypeCode);
                if (existingType == null)
                {
                    existingType = new DocumentType(
                        _guidGenerator.Create(),
                        tenantId: null,
                        typeDef.TypeCode,
                        resolvedDisplayName,
                        typeDef.ConfidenceThreshold,
                        typeDef.Priority);
                    await _documentTypeRepository.InsertAsync(existingType, autoSave: true);
                }
                else
                {
                    existingType.Update(resolvedDisplayName, typeDef.ConfidenceThreshold, typeDef.Priority);
                    await _documentTypeRepository.UpdateAsync(existingType, autoSave: true);
                }

                // Host 字段 upsert
                foreach (var hostField in typeDef.Fields)
                {
                    var existingField = await _fieldDefinitionRepository.FindByNameAsync(
                        null, typeDef.TypeCode, hostField.Name);

                    if (existingField == null)
                    {
                        var newField = new FieldDefinition(
                            _guidGenerator.Create(),
                            tenantId: null,
                            typeDef.TypeCode,
                            hostField.Name,
                            hostField.Prompt,
                            hostField.DataType,
                            displayOrder: typeDef.Fields.ToList().IndexOf(hostField),
                            isRequired: hostField.Required);
                        await _fieldDefinitionRepository.InsertAsync(newField, autoSave: true);
                    }
                    else
                    {
                        existingField.Update(
                            hostField.Prompt,
                            hostField.DataType,
                            displayOrder: typeDef.Fields.ToList().IndexOf(hostField),
                            isRequired: hostField.Required);
                        await _fieldDefinitionRepository.UpdateAsync(existingField, autoSave: true);
                    }
                }
            }
        }
    }
}
