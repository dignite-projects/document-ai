using System;

namespace Dignite.Paperbase.Mcp.Documents;

/// <summary>
/// MCP 文档类型资源 URI 的单一来源——资源模板（read 路径）与 resources/list 动态枚举共用同一 scheme，
/// 防止多处手写 <c>paperbase://document-types/...</c> 漂移。与 <see cref="DocumentResourceUri"/>（文档资源）对称：
/// 文档按 id 寻址（数量无限、不枚举、走检索 tool 发现）；文档类型按 code 寻址（数量有限、resources/list 枚举发现）。
/// </summary>
public static class DocumentTypeResourceUri
{
    private const string Prefix = "paperbase://document-types/";

    /// <summary>资源 URI 模板。用于 <c>[McpServerResource(UriTemplate = ...)]</c>，必须是编译期常量。</summary>
    public const string Template = Prefix + "{code}";

    public static string Format(string typeCode) => Prefix + typeCode;
}
