using System;
using System.Threading.Tasks;
using Dignite.Paperbase.Documents;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Paperbase.EntityFrameworkCore.Documents;

/// <summary>
/// <see cref="EfCoreDocumentRepository.GetFieldMatchedIdsAsync"/> 的 loud fail-closed 行为
/// （Issue #204 + 验证上移重构）。覆盖在 SQLite 上可运行的路径——即执行 SQL Server <c>JSON_VALUE</c>
/// 原始 SQL **之前**就短路 / 抛错的分支（空入参、字段名白名单、区间类型拒绝、值类型不符）。
/// 输入结构校验（必填 / 长度 / 数量 / 至少一个值）已上移到 Application 层 DTO（<c>AbpValidationException</c>），
/// 不在仓储重复，故这里不测。类型化字段值的真正 SQL 匹配（<c>JSON_VALUE</c>/<c>TRY_CONVERT</c>）
/// 由 <see cref="EfCoreDocumentRepositoryFieldPredicate_Tests"/> 在内存中按生成的 SQL + 参数断言。
/// </summary>
public class EfCoreDocumentRepositorySearch_Tests : PaperbaseEntityFrameworkCoreTestBase
{
    private readonly IDocumentRepository _documentRepository;

    public EfCoreDocumentRepositorySearch_Tests()
    {
        _documentRepository = GetRequiredService<IDocumentRepository>();
    }

    [Fact]
    public async Task Empty_field_queries_returns_empty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // 调用层只在有字段过滤器时调用；防御空入参 → 空集合（不拼空 WHERE）。
            var ids = await _documentRepository.GetFieldMatchedIdsAsync(
                "contract.general", Array.Empty<DocumentFieldQuery>());

            ids.ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task Illegal_field_name_throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // 字段名含白名单外字符 → 纵深防御抛可纠正错误（loud），不把可疑输入透传到 SQL。
            var ex = await Should.ThrowAsync<BusinessException>(() => _documentRepository.GetFieldMatchedIdsAsync(
                "contract.general",
                new[] { new DocumentFieldQuery("bad name!", FieldDataType.String, FieldValue: "x") }));

            ex.Code.ShouldBe(PaperbaseErrorCodes.InvalidExtractedFieldName);
        });
    }

    [Fact]
    public async Task Range_on_string_field_throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // String 字段只认等值；传区间在构造谓词时抛可纠正信号（类型由调用层解析后传入）。
            var ex = await Should.ThrowAsync<BusinessException>(() => _documentRepository.GetFieldMatchedIdsAsync(
                "contract.general",
                new[] { new DocumentFieldQuery("party", FieldDataType.String, FieldValueMin: "a", FieldValueMax: "z") }));

            ex.Code.ShouldBe(PaperbaseErrorCodes.FieldTypeDoesNotSupportRange);
        });
    }

    [Fact]
    public async Task Value_not_matching_declared_type_throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Integer 字段传 "abc" → 值无法解析为声明类型 → loud fail（之前是静默空）。
            var ex = await Should.ThrowAsync<BusinessException>(() => _documentRepository.GetFieldMatchedIdsAsync(
                "contract.general",
                new[] { new DocumentFieldQuery("count", FieldDataType.Integer, FieldValue: "abc") }));

            ex.Code.ShouldBe(PaperbaseErrorCodes.InvalidExtractedFieldValue);
        });
    }
}
