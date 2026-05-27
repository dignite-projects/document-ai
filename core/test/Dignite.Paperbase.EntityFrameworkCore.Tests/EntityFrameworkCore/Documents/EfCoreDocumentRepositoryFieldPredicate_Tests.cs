using System;
using Dignite.Paperbase.Documents;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Paperbase.EntityFrameworkCore.Documents;

/// <summary>
/// <see cref="EfCoreDocumentRepository.BuildFieldPredicate"/> 的类型化分派单元测试（Issue #204 任务 3）。
/// SQLite 测试库无法执行 SQL Server <c>JSON_VALUE</c>/<c>TRY_CONVERT</c>，所以「按 <see cref="FieldDataType"/>
/// 生成什么 SQL + 什么参数、何时拒绝区间、脏入参何时降级」这套分派逻辑在内存中直接断言生成结果
/// （internal 经 InternalsVisibleTo 可见）。红线：只 = + range，永不 LIKE。
/// </summary>
public class EfCoreDocumentRepositoryFieldPredicate_Tests
{
    // ─── String：仅等值，无 TRY_CONVERT，永不 LIKE ───────────────────────────

    [Fact]
    public void String_equality_emits_plain_json_value_comparison()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.String, "party", fieldValue: "Acme", fieldValueMin: null, fieldValueMax: null);

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe("JSON_VALUE([ExtractedFields], '$.\"party\"') = {0}");
        p.Value.WhereClause.ShouldNotContain("LIKE");
        p.Value.WhereClause.ShouldNotContain("TRY_CONVERT");
        p.Value.Parameters.ShouldBe(new object[] { "Acme" });
    }

    [Fact]
    public void String_range_is_rejected()
    {
        var ex = Should.Throw<BusinessException>(() => EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.String, "party", fieldValue: null, fieldValueMin: "a", fieldValueMax: "z"));

        ex.Code.ShouldBe(PaperbaseErrorCodes.FieldTypeDoesNotSupportRange);
    }

    // ─── Boolean：等值，归一化为 JSON true/false 文本；非布尔入参降级 null ─────

    [Fact]
    public void Boolean_equality_normalizes_input()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Boolean, "active", fieldValue: "True", fieldValueMin: null, fieldValueMax: null);

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe("JSON_VALUE([ExtractedFields], '$.\"active\"') = {0}");
        p.Value.Parameters.ShouldBe(new object[] { "true" });
    }

    [Fact]
    public void Boolean_non_boolean_input_degrades_to_null()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Boolean, "active", fieldValue: "yes", fieldValueMin: null, fieldValueMax: null);

        p.ShouldBeNull();
    }

    [Fact]
    public void Boolean_range_is_rejected()
    {
        var ex = Should.Throw<BusinessException>(() => EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Boolean, "active", fieldValue: null, fieldValueMin: "false", fieldValueMax: "true"));

        ex.Code.ShouldBe(PaperbaseErrorCodes.FieldTypeDoesNotSupportRange);
    }

    // ─── Integer：TRY_CONVERT(bigint) 等值/区间；脏入参降级 null ──────────────

    [Fact]
    public void Integer_equality_converts_to_bigint_with_long_parameter()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Integer, "count", fieldValue: "3", fieldValueMin: null, fieldValueMax: null);

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe(
            "TRY_CONVERT(bigint, JSON_VALUE([ExtractedFields], '$.\"count\"')) = {0}");
        p.Value.Parameters.ShouldBe(new object[] { 3L });
    }

    [Fact]
    public void Integer_unparseable_input_degrades_to_null()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Integer, "count", fieldValue: "abc", fieldValueMin: null, fieldValueMax: null);

        p.ShouldBeNull();
    }

    // ─── Decimal：TRY_CONVERT(decimal(38,6)) 区间（含界） ────────────────────

    [Fact]
    public void Decimal_inclusive_range_emits_both_bounds()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Decimal, "amount", fieldValue: null, fieldValueMin: "100", fieldValueMax: "200.5");

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe(
            "TRY_CONVERT(decimal(38,6), JSON_VALUE([ExtractedFields], '$.\"amount\"')) >= {0} AND " +
            "TRY_CONVERT(decimal(38,6), JSON_VALUE([ExtractedFields], '$.\"amount\"')) <= {1}");
        p.Value.Parameters.ShouldBe(new object[] { 100m, 200.5m });
    }

    [Fact]
    public void Decimal_range_min_only_emits_lower_bound()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Decimal, "amount", fieldValue: null, fieldValueMin: "100", fieldValueMax: null);

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe(
            "TRY_CONVERT(decimal(38,6), JSON_VALUE([ExtractedFields], '$.\"amount\"')) >= {0}");
        p.Value.Parameters.ShouldBe(new object[] { 100m });
    }

    [Fact]
    public void Decimal_range_with_unparseable_bound_degrades_to_null()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Decimal, "amount", fieldValue: null, fieldValueMin: "100", fieldValueMax: "lots");

        p.ShouldBeNull();
    }

    // ─── Date / DateTime：TRY_CONVERT(date|datetime2) + 强类型参数 ───────────

    [Fact]
    public void Date_equality_converts_to_date_with_datetime_parameter()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Date, "signed_on", fieldValue: "2024-01-15", fieldValueMin: null, fieldValueMax: null);

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe(
            "TRY_CONVERT(date, JSON_VALUE([ExtractedFields], '$.\"signed_on\"')) = {0}");
        p.Value.Parameters.ShouldBe(new object[] { new DateTime(2024, 1, 15) });
    }

    [Fact]
    public void DateTime_range_converts_to_datetime2()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.DateTime, "created", fieldValue: null,
            fieldValueMin: "2024-01-01T00:00:00", fieldValueMax: "2024-12-31T23:59:59");

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe(
            "TRY_CONVERT(datetime2, JSON_VALUE([ExtractedFields], '$.\"created\"')) >= {0} AND " +
            "TRY_CONVERT(datetime2, JSON_VALUE([ExtractedFields], '$.\"created\"')) <= {1}");
        p.Value.Parameters.ShouldBe(new object[]
        {
            new DateTime(2024, 1, 1, 0, 0, 0),
            new DateTime(2024, 12, 31, 23, 59, 59)
        });
    }

    [Fact]
    public void DateTime_offset_free_equality_is_accepted_as_wall_clock()
    {
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.DateTime, "created", fieldValue: "2024-01-01T10:00:00",
            fieldValueMin: null, fieldValueMax: null);

        p.ShouldNotBeNull();
        p!.Value.Parameters.ShouldBe(new object[] { new DateTime(2024, 1, 1, 10, 0, 0) });
    }

    [Theory]
    [InlineData("2024-01-01T10:00:00+08:00")]   // 显式偏移
    [InlineData("2024-01-01T10:00:00Z")]        // UTC 'Z'
    public void DateTime_offset_bearing_input_degrades_to_null(string offsetInput)
    {
        // 带时区的入参会被 .NET 换算到服务器本地时区、与 datetime2 的 wall-clock 语义不一致——
        // 判脏入参降级 null（Codex 评审 finding 2）。
        EfCoreDocumentRepository.BuildFieldPredicate(
                FieldDataType.DateTime, "created", fieldValue: offsetInput,
                fieldValueMin: null, fieldValueMax: null)
            .ShouldBeNull();
    }

    // ─── parameterOffset：多字段拼接时占位符按全局参数偏移排号，不错位 ──────────

    [Fact]
    public void Parameter_offset_shifts_equality_placeholder()
    {
        // 第 2 个字段（前面已累积 3 个参数）的等值占位符应从 {3} 起，而非 {0}。
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.String, "party", fieldValue: "Acme",
            fieldValueMin: null, fieldValueMax: null, parameterOffset: 3);

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe("JSON_VALUE([ExtractedFields], '$.\"party\"') = {3}");
        p.Value.Parameters.ShouldBe(new object[] { "Acme" });
    }

    [Fact]
    public void Parameter_offset_shifts_range_placeholders()
    {
        // 区间两界占位符从 offset 起连续排号（{2}/{3}），参数顺序与之对应。
        var p = EfCoreDocumentRepository.BuildFieldPredicate(
            FieldDataType.Decimal, "amount", fieldValue: null,
            fieldValueMin: "100", fieldValueMax: "200", parameterOffset: 2);

        p.ShouldNotBeNull();
        p!.Value.WhereClause.ShouldBe(
            "TRY_CONVERT(decimal(38,6), JSON_VALUE([ExtractedFields], '$.\"amount\"')) >= {2} AND " +
            "TRY_CONVERT(decimal(38,6), JSON_VALUE([ExtractedFields], '$.\"amount\"')) <= {3}");
        p.Value.Parameters.ShouldBe(new object[] { 100m, 200m });
    }
}
