namespace Dignite.Paperbase.Documents;

public static class DocumentExtractedFieldConsts
{
    /// <summary>
    /// 类型绑定字段 <c>String</c> 值的最大长度。
    /// <para>
    /// 同时是 DB 列长度（<c>StringValue nvarchar(256)</c>）与 App 层校验上限（<c>ExtractedFieldValueValidator</c>）——
    /// 二者必须一致：校验放过的值都要存得下。改动此值需重新生成 EF migration。
    /// </para>
    /// <para>
    /// 限长（而非 <c>nvarchar(max)</c>）让 <c>StringValue</c> 能进 <c>(TenantId, FieldDefinitionId, StringValue,
    /// DocumentId)</c> 复合索引键，String 字段等值查询走纯 index seek（#209）。类型绑定 String 字段是从 Markdown
    /// 抽取的结构化短值（姓名 / 编号 / 币种 / 案由等），不承载长文本（长文本归 <c>Document.Markdown</c>），故 256
    /// 是充裕上限；且 256×2 bytes + 三个 Guid（48 bytes）= 560 bytes，安全落在 SQL Server 非聚集索引键 1700 bytes 上限内。
    /// </para>
    /// </summary>
    public static int MaxStringValueLength { get; set; } = 256;
}
