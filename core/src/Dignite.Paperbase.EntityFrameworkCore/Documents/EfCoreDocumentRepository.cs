using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Paperbase;
using Dignite.Paperbase.Documents;
using Dignite.Paperbase.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Paperbase.Documents;

public class EfCoreDocumentRepository
    : EfCoreRepository<PaperbaseDbContext, Document, Guid>, IDocumentRepository
{
    // ExtractedFields 字段名白名单——与 FieldDefinitionConsts.NamePattern 同源（^[A-Za-z0-9_\-]{1,64}$）。
    // 校验后字符集不含 " / \，可安全引号化为 JSON path key（$."name"）。
    private static readonly Regex ExtractedFieldNameRegex =
        new(FieldDefinitionConsts.NamePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // TRY_CONVERT 目标类型——Decimal 字段用 decimal(38,6)：32 位整数 + 6 位小数，
    // 覆盖任何现实抽取金额而不溢出/截断。JSON 索引（#198）GA 后另起 migration 时与此对齐。
    private const string DecimalSqlType = "decimal(38,6)";

    public EfCoreDocumentRepository(
        IDbContextProvider<PaperbaseDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<Document?> FindByBlobNameAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .FirstOrDefaultAsync(
                d => d.OriginalFileBlobName == blobName,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<Document?> FindByContentHashAsync(
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        using (DataFilter.Disable<ISoftDelete>())
        {
            var dbSet = await GetDbSetAsync();
            return await dbSet
                .FirstOrDefaultAsync(
                    d => d.FileOrigin.ContentHash == contentHash,
                    GetCancellationToken(cancellationToken));
        }
    }

    public override async Task<IQueryable<Document>> WithDetailsAsync()
    {
        return (await GetQueryableAsync()).IncludeDetails();
    }

    public virtual async Task HardDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        await dbContext.Set<Document>()
            .IgnoreQueryFilters()
            .Where(d => d.Id == id)
            .ExecuteDeleteAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<Guid>> GetFieldMatchedIdsAsync(
        string documentTypeCode,
        IReadOnlyList<DocumentFieldQuery> fieldQueries,
        CancellationToken cancellationToken = default)
    {
        // 调用层（DocumentAppService.GetListAsync）仅在有字段过滤器时调用，且已校验 documentTypeCode 必填、
        // 字段数量 / 长度 / 至少一个值（DTO + AppService 层，loud AbpValidationException）。此处防御空入参。
        if (fieldQueries is not { Count: > 0 })
        {
            return new List<Guid>();
        }

        var dbSet = await GetDbSetAsync();

        // ExtractedFields 是 Dictionary<string,JsonElement> 经 ValueConverter 映射为 native json 列，
        // EF Core 10 无法 LINQ 翻译动态键查询（d.ExtractedFields["x"] 不可译）。用固定模板参数化 raw SQL
        // 走 JSON_VALUE + TRY_CONVERT 按类型分派；多字段各子句占位符按全局参数偏移排号、AND 拼接。
        // 这不是 llm-call-anti-patterns.md 反例 5 的"LLM 拼 SQL"——形状固定，仅受限 path + 参数化 value 可变。
        // 注：SQL Server 2025 CREATE JSON INDEX 仍 preview（#198），range 暂为全表扫，查询形状已正确。
        // documentTypeCode 作为 SQL 参数 {0} 锚定单一类型（字段值离开类型无确定含义），字段谓词从 {1} 起。
        var parameters = new List<object> { documentTypeCode };
        var clauses = new List<string>(fieldQueries.Count);

        foreach (var fieldQuery in fieldQueries)
        {
            // 纵深防御：字段名进 JSON path 前的白名单。输入格式校验已在 Application 层（DTO RegularExpression）
            // 完成，此处是 raw SQL 拼接点的最后防线——违例抛可纠正错误（loud），不静默放行可疑输入。
            if (!ExtractedFieldNameRegex.IsMatch(fieldQuery.FieldName))
            {
                throw new BusinessException(PaperbaseErrorCodes.InvalidExtractedFieldName)
                    .WithData("FieldName", fieldQuery.FieldName);
            }

            // 按调用层解析好的 DataType 分派出 WHERE 子句 + 参数；占位符按已累积参数数偏移排号。
            // String/Boolean 传区间抛 FieldTypeDoesNotSupportRange；值无法解析为声明类型 → null。
            var predicate = BuildFieldPredicate(
                fieldQuery.FieldDataType,
                fieldQuery.FieldName,
                fieldQuery.FieldValue,
                fieldQuery.FieldValueMin,
                fieldQuery.FieldValueMax,
                parameterOffset: parameters.Count);
            if (predicate == null)
            {
                // 值与声明类型不符（如 Integer 字段传 "abc"）——loud fail（可纠正信号），不静默空结果。
                throw new BusinessException(PaperbaseErrorCodes.InvalidExtractedFieldValue)
                    .WithData("FieldName", fieldQuery.FieldName)
                    .WithData("DocumentTypeCode", documentTypeCode)
                    .WithData("DataType", fieldQuery.FieldDataType.ToString());
            }

            clauses.Add($"({predicate.Value.WhereClause})");
            parameters.AddRange(predicate.Value.Parameters);
        }

        var table = string.IsNullOrEmpty(PaperbaseDbProperties.DbSchema)
            ? $"[{PaperbaseDbProperties.DbTablePrefix}Documents]"
            : $"[{PaperbaseDbProperties.DbSchema}].[{PaperbaseDbProperties.DbTablePrefix}Documents]";

        // 多字段之间取 AND（结构化检索惯例：不同字段互相收窄），全部锚定同一 documentTypeCode。
        // 租户隔离 + 软删除范围由 ABP 全局查询过滤器按 ambient 状态自动施加（FromSqlRaw 经 EF Core 子查询包装）。
        var sql = $"SELECT * FROM {table} WHERE [DocumentTypeCode] = {{0}} AND {string.Join(" AND ", clauses)}";

        return await dbSet
            .FromSqlRaw(sql, parameters.ToArray())
            .AsNoTracking()
            .Select(d => d.Id)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    // internal（非 private）以便 EF.Tests 直接单元测试谓词分派——见 csproj InternalsVisibleTo。
    internal readonly record struct FieldPredicate(string WhereClause, object[] Parameters);

    /// <summary>
    /// 按 <see cref="FieldDataType"/> 把字段值查询编译成"固定形状 + 参数化"的 WHERE 子句：
    /// <list type="bullet">
    ///   <item><c>String</c> / <c>Boolean</c>：仅等值 <c>=</c>（红线：永不 LIKE）；传区间 → 抛
    ///   <see cref="PaperbaseErrorCodes.FieldTypeDoesNotSupportRange"/>（给 AI 客户端可纠正信号）。</item>
    ///   <item><c>Integer</c> / <c>Decimal</c> / <c>Date</c> / <c>DateTime</c>：<c>TRY_CONVERT</c> 后等值或区间
    ///   （含界）。脏存值转换失败自然不命中；脏入参解析失败返回 null → 调用方 fail-closed 空结果。</item>
    /// </list>
    /// 与 SQL Server 2025 JSON 索引支持算子（=、BETWEEN/IN）对齐。
    /// <paramref name="parameterOffset"/> 为生成的 <c>{n}</c> 占位符起始下标——多字段拼接时由调用方传入
    /// 已累积参数数，使各子句占位符在最终合并参数数组里全局唯一、不错位（单字段默认 0）。
    /// </summary>
    internal static FieldPredicate? BuildFieldPredicate(
        FieldDataType dataType,
        string fieldName,
        string? fieldValue,
        string? fieldValueMin,
        string? fieldValueMax,
        int parameterOffset = 0)
    {
        // 引号化 JSON path key（fieldName 已白名单校验，无 " / \）。
        var jsonValue = $"JSON_VALUE([ExtractedFields], '$.\"{fieldName}\"')";

        var isEquality = fieldValue != null;
        var isRange = !isEquality && (fieldValueMin != null || fieldValueMax != null);

        switch (dataType)
        {
            case FieldDataType.String:
                if (isRange)
                {
                    throw RangeNotSupported(fieldName, dataType);
                }
                return new FieldPredicate($"{jsonValue} = {{{parameterOffset}}}", [fieldValue!]);

            case FieldDataType.Boolean:
                if (isRange)
                {
                    throw RangeNotSupported(fieldName, dataType);
                }
                // JSON 布尔存为 true/false；JSON_VALUE 返回 "true"/"false"。归一化调用方输入再比较。
                if (!bool.TryParse(fieldValue, out var boolValue))
                {
                    return null;
                }
                return new FieldPredicate($"{jsonValue} = {{{parameterOffset}}}", [boolValue ? "true" : "false"]);

            case FieldDataType.Integer:
                return BuildComparablePredicate(
                    $"TRY_CONVERT(bigint, {jsonValue})",
                    isEquality, fieldValue, fieldValueMin, fieldValueMax, parameterOffset,
                    static s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                        ? v
                        : (long?)null);

            case FieldDataType.Decimal:
                return BuildComparablePredicate(
                    $"TRY_CONVERT({DecimalSqlType}, {jsonValue})",
                    isEquality, fieldValue, fieldValueMin, fieldValueMax, parameterOffset,
                    static s => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
                        ? v
                        : (decimal?)null);

            case FieldDataType.Date:
                return BuildComparablePredicate(
                    $"TRY_CONVERT(date, {jsonValue})",
                    isEquality, fieldValue, fieldValueMin, fieldValueMax, parameterOffset,
                    static s => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v)
                        ? v.Date
                        : (DateTime?)null);

            case FieldDataType.DateTime:
                return BuildComparablePredicate(
                    $"TRY_CONVERT(datetime2, {jsonValue})",
                    isEquality, fieldValue, fieldValueMin, fieldValueMax, parameterOffset,
                    // 只认无偏移的 wall-clock ISO 串（与存储侧 datetime2 一致）。带偏移 / Z 的串会被 .NET
                    // 换算到服务器本地时区、与 TRY_CONVERT(datetime2) 保留 wall-clock 的语义不一致，
                    // 比较结果会随服务器时区漂移——判脏入参返回 null（Codex 评审 finding 2）。
                    // DateTimeKind.Unspecified 表示输入未携带时区信息。
                    static s => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v)
                                && v.Kind == DateTimeKind.Unspecified
                        ? v
                        : (DateTime?)null);

            default:
                return null;
        }
    }

    /// <summary>
    /// 数字 / 日期类字段的等值或区间（含界）谓词构造。<paramref name="parse"/> 把调用方字符串入参解析为
    /// 强类型 CLR 值（作为 SQL 参数，避免拼接）；任一入参解析失败返回 null → 调用方 fail-closed 空结果。
    /// </summary>
    private static FieldPredicate? BuildComparablePredicate<T>(
        string convertExpr,
        bool isEquality,
        string? fieldValue,
        string? fieldValueMin,
        string? fieldValueMax,
        int parameterOffset,
        Func<string, T?> parse) where T : struct
    {
        if (isEquality)
        {
            var parsed = parse(fieldValue!);
            return parsed == null
                ? null
                : new FieldPredicate($"{convertExpr} = {{{parameterOffset}}}", [parsed.Value]);
        }

        var clauses = new List<string>(2);
        var parameters = new List<object>(2);

        if (fieldValueMin != null)
        {
            var min = parse(fieldValueMin);
            if (min == null)
            {
                return null;
            }
            clauses.Add($"{convertExpr} >= {{{parameterOffset + parameters.Count}}}");
            parameters.Add(min.Value);
        }

        if (fieldValueMax != null)
        {
            var max = parse(fieldValueMax);
            if (max == null)
            {
                return null;
            }
            clauses.Add($"{convertExpr} <= {{{parameterOffset + parameters.Count}}}");
            parameters.Add(max.Value);
        }

        return clauses.Count == 0
            ? null
            : new FieldPredicate(string.Join(" AND ", clauses), parameters.ToArray());
    }

    private static BusinessException RangeNotSupported(string fieldName, FieldDataType dataType) =>
        new BusinessException(PaperbaseErrorCodes.FieldTypeDoesNotSupportRange)
            .WithData("FieldName", fieldName)
            .WithData("DataType", dataType.ToString());
}
