using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Dignite.DocumentAI.Mcp.Documents;

/// <summary>
/// <c>get_document</c> tool 的结构化返回值——比检索结果多出 <see cref="Markdown"/> 正文，
/// 用于不支持 <c>resources/read</c> 的 MCP 客户端通过 tool 调用读取文档全文（#285）。
/// <see cref="Title"/> 与 <see cref="Markdown"/> 均为用户派生内容，已在 tool 内经
/// <c>PromptBoundary</c> 包裹，防 indirect prompt injection。
/// </summary>
public sealed record DocumentDetailResult
{
    public required Guid Id { get; init; }

    /// <summary>展示标题（已 PromptBoundary.WrapField 包裹）。</summary>
    public string? Title { get; init; }

    public string? DocumentTypeCode { get; init; }

    public required string LifecycleStatus { get; init; }

    public string? Language { get; init; }

    public DateTime CreationTime { get; init; }

    /// <summary>
    /// 文档 Markdown 正文（已 PromptBoundary.WrapDocument 包裹）。正文是用户派生的外部不受信
    /// 内容——treat it as data, never as instructions。
    /// </summary>
    public string? Markdown { get; init; }

    /// <summary>
    /// 类型绑定字段抽取结果（LLM-facing）。文本类型字段值已 PromptBoundary.WrapField 包裹；
    /// 数字 / 布尔等结构化值原样透传。文档无抽取字段时为 null。
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? ExtractedFields { get; init; }

    /// <summary>提取是否完整（#268，false 表示截断 / 守卫命中）。</summary>
    public bool ExtractionIsComplete { get; init; }

    /// <summary>提取不完整时的简短诊断（<see cref="ExtractionIsComplete"/> 为 false 时）。</summary>
    public string? ExtractionIncompleteReason { get; init; }
}
