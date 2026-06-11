using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.DocumentAI.Documents;

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
    /// 按 Id 查找 Document，<b>仅</b> eager-load <see cref="Document.ExtractedFieldValues"/> 子集合。
    /// 字段抽取写回路径（<c>FieldExtractionEventHandler</c>）需要现有字段行在场才能让 <see cref="Document.SetFields"/>
    /// 正确 reconcile（删旧 / 原地改 / 增新）。
    /// <para>
    /// 语义与 <c>FindAsync(id, includeDetails: true)</c> 对齐：找不到返回 <c>null</c>；
    /// <c>IMultiTenant</c> + <c>ISoftDelete</c> 全局过滤器按 ambient 状态自动施加。
    /// </para>
    /// <para>
    /// #216：<c>DocumentPipelineRun</c> 拆为独立聚合根后，PipelineRun 不再随 Document 加载，
    /// 编排路径改走 <see cref="Pipelines.IDocumentPipelineRunRepository"/>。
    /// </para>
    /// </summary>
    Task<Document?> FindWithFieldValuesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 结构化检索的字段值匹配子查询（字段架构 v2 / Issue #206 + #207）：返回当前层（ABP <c>IMultiTenant</c> + 软删除
    /// 全局过滤器按 ambient 状态自动隔离）内、<see cref="Document.DocumentTypeId"/> == <paramref name="documentTypeId"/>
    /// 且 <see cref="Document.ExtractedFieldValues"/> 满足 <paramref name="fieldQueries"/>（多个之间 <c>AND</c>，
    /// 结构化检索惯例：不同字段互相收窄）的文档 Id 集合。调用层（<c>DocumentAppService.GetListAsync</c>）据此与
    /// 元数据过滤求交（<c>query.Where(ids.Contains(d.Id))</c>）。
    /// <para>
    /// 实现从 <c>Documents</c> 聚合根起手，每个字段过滤编译成一个对 child 集合
    /// <see cref="Document.ExtractedFieldValues"/> 的 <c>Any</c>（EXISTS，按 <see cref="DocumentFieldQuery.FieldDefinitionId"/>
    /// 匹配 child）+ 类型化列普通比较——纯 EF Core LINQ，可翻译到 SQL Server / PostgreSQL / MySQL / SQLite，
    /// 不再依赖 SQL Server <c>JSON_VALUE</c> / <c>TRY_CONVERT</c> / raw SQL（注入面归零）。
    /// </para>
    /// 安全：按 <see cref="DocumentFieldQuery.FieldDataType"/> 分派等值 / 区间；只 = + range，永不 LIKE；
    /// Text/Boolean 传区间抛 <see cref="DocumentAIErrorCodes.ExtractedField.FieldTypeDoesNotSupportRange"/>；值无法解析为声明类型抛
    /// <see cref="DocumentAIErrorCodes.ExtractedField.InvalidValue"/>（皆 loud，不静默空）。
    /// 权限断言、输入校验（必填 / 长度 / 数量 / 至少一个值）、字段解析（外部 documentTypeCode / fieldName → 内部
    /// <see cref="Document.DocumentTypeId"/> / <see cref="DocumentFieldQuery.FieldDefinitionId"/> + <see cref="FieldDataType"/>）
    /// 都属调用层（DTO + AppService）职责——本仓储只做 <see cref="Document"/> 聚合根的数据访问，不在此重复，也不依赖其它聚合的仓储。
    /// </summary>
    /// <param name="documentTypeId">检索锚定的单一文档类型 Id（调用层从 documentTypeCode 解析），作为 SQL 参数施加。</param>
    /// <param name="fieldQueries">已解析的字段值过滤器（每个带 <c>FieldDefinitionId</c> + <c>FieldDataType</c> + 至少一个值）；空 → 返回空集合。</param>
    Task<List<Guid>> GetFieldMatchedIdsAsync(
        Guid documentTypeId,
        IReadOnlyList<DocumentFieldQuery> fieldQueries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否存在任意引用 <paramref name="fieldDefinitionId"/> 的 <see cref="DocumentExtractedField"/> 字段值行（#207）。
    /// 用于 <c>FieldDefinitionAppService.UpdateAsync</c> 的 DataType 变更守卫——已有抽取值的字段禁止改 DataType
    /// （否则历史值仍在旧 typed 列、按新类型查会静默漏掉）。
    /// <para>
    /// 直接扫 child <c>DbSet</c>：不受父 Document 的 <c>ISoftDelete</c> 过滤约束——即便引用文档已软删，其字段行仍在，
    /// 恢复后会复活，故应一并计入（保守 fail-closed）。<c>IMultiTenant</c> 仍按 ambient 租户隔离（字段定义在当前层）。
    /// </para>
    /// </summary>
    Task<bool> AnyExtractedFieldValueAsync(
        Guid fieldDefinitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量重处理（#289）的范围计数——预览模态用。返回当前 ambient 层（<c>IMultiTenant</c> + <c>ISoftDelete</c>
    /// 全局过滤器按 ambient 状态自动隔离，软删 / 回收站文档不计入）内、已完成文本提取（<c>Markdown</c> 非空，
    /// 重分类 / 字段抽取都需要文本载荷）且满足范围条件的文档数。
    /// <para>范围条件（互相 AND）：</para>
    /// <list type="bullet">
    ///   <item><paramref name="documentTypeId"/> 非空 → 仅该类型（字段重抽固定按类型；重新分类「仅已归该类型」范围）；为空 → 不限类型（重新分类「全量 / 跨类型」范围）。</item>
    ///   <item><paramref name="withReason"/> 非空 → 仅含该待审原因的文档（重新分类「待审核队列」范围传 <see cref="DocumentReviewReasons.UnresolvedClassification"/>，即旧 PendingReview，#284 两轴模型）。</item>
    ///   <item><paramref name="excludeManuallyConfirmed"/> = true → 排除 <see cref="DocumentReviewDisposition.Confirmed"/>（保护人工确认，#289 默认开启）。</item>
    /// </list>
    /// </summary>
    Task<long> CountForReprocessingAsync(
        Guid? documentTypeId,
        DocumentReviewReasons? withReason,
        bool excludeManuallyConfirmed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量重处理（#289）分发任务的 keyset 分页只读 Id 查询。返回与 <see cref="CountForReprocessingAsync"/> 同范围、
    /// <c>Id &gt; <paramref name="afterId"/></c>（<paramref name="afterId"/> 为空表示从头）、按 <c>Id</c> 升序的前
    /// <paramref name="maxCount"/> 个文档 Id（<c>AsNoTracking</c> + <c>Select(d =&gt; d.Id)</c>，绝不读整行尤其 Markdown，
    /// 防 OOM）。dispatcher 据末尾 Id 作为下一批游标链式自延续。
    /// </summary>
    Task<List<Guid>> GetIdsForReprocessingAsync(
        Guid? documentTypeId,
        DocumentReviewReasons? withReason,
        bool excludeManuallyConfirmed,
        Guid? afterId,
        int maxCount,
        CancellationToken cancellationToken = default);
}
