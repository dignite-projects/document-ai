using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Paperbase.Documents;

public interface IDocumentRepository : IRepository<Document, Guid>
{
    Task<Document?> FindByBlobNameAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    Task<Document?> FindByContentHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default);

    Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 结构化检索的字段值匹配子查询：返回当前层（ABP <c>IMultiTenant</c> + 软删除全局过滤器按 ambient 状态
    /// 自动隔离）内、<see cref="Document.DocumentTypeCode"/> == <paramref name="documentTypeCode"/> 且
    /// ExtractedFields 满足 <paramref name="fieldQueries"/>（多个之间 <c>AND</c>，结构化检索惯例：不同字段互相收窄）
    /// 的文档 Id 集合。调用层（<c>DocumentAppService.GetListAsync</c>）据此与元数据过滤求交
    /// （<c>query.Where(ids.Contains(d.Id))</c>）——把"动态 JSON 键查询"这件 EF Core 10 无法 LINQ 翻译的事
    /// 隔离在仓储内（<c>FromSqlRaw</c> + <c>JSON_VALUE</c> + <c>TRY_CONVERT</c>），不向上层泄漏 <c>IQueryable</c>。
    /// 安全：
    /// <list type="bullet">
    ///   <item>每个 <see cref="DocumentFieldQuery.FieldName"/> 进 JSON path 前按 <c>FieldDefinitionConsts.NamePattern</c>
    ///   白名单校验（纵深防御；违例抛 <see cref="PaperbaseErrorCodes.InvalidExtractedFieldName"/>），值与 typeCode 一律走 SQL 参数；</item>
    ///   <item>按 <see cref="DocumentFieldQuery.FieldDataType"/> 分派 <c>TRY_CONVERT</c> 等值/区间；只 = + range，永不 LIKE；
    ///   String/Boolean 传区间抛 <see cref="PaperbaseErrorCodes.FieldTypeDoesNotSupportRange"/>；
    ///   值无法解析为声明类型抛 <see cref="PaperbaseErrorCodes.InvalidExtractedFieldValue"/>（皆 loud，不静默空）。</item>
    /// </list>
    /// 权限断言、输入校验（必填 / 长度 / 数量 / 至少一个值）、字段类型解析（<c>FieldDefinition</c> → <see cref="FieldDataType"/>）
    /// 都属调用层（DTO + AppService）职责——本仓储只做 <see cref="Document"/> 聚合根的数据访问，不在此重复，也不依赖其它聚合的仓储。
    /// </summary>
    /// <param name="documentTypeCode">检索锚定的单一文档类型（调用层已校验非空），作为 SQL 参数施加。</param>
    /// <param name="fieldQueries">已解析的字段值过滤器（每个带 <c>FieldName</c> + <c>FieldDataType</c> + 至少一个值）；空 → 返回空集合。</param>
    Task<List<Guid>> GetFieldMatchedIdsAsync(
        string documentTypeCode,
        IReadOnlyList<DocumentFieldQuery> fieldQueries,
        CancellationToken cancellationToken = default);
}
