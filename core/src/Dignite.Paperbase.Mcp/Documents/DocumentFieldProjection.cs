using System.Collections.Generic;
using System.Text.Json;
using Dignite.Paperbase.Ai;

namespace Dignite.Paperbase.Mcp.Documents;

/// <summary>
/// ExtractedFields 的 LLM-facing 投影逻辑——在检索 tool 和 get_document tool 之间共享，
/// 保证两处的 PromptBoundary 包裹规则完全一致（安全规则的单一实现来源）。
/// </summary>
internal static class DocumentFieldProjection
{
    /// <summary>
    /// 把文档的 ExtractedFields（原样 <see cref="JsonElement"/>）转成 LLM-facing 投影，保留声明类型：
    /// 数字 / 布尔等结构化值原样透传；String 类型值经 <c>PromptBoundary.WrapField</c> 包裹防
    /// indirect prompt injection；JSON null 跳过不投影；全部跳过 / 无字段 → 返回 null。
    /// </summary>
    internal static IReadOnlyDictionary<string, JsonElement>? Project(
        IReadOnlyDictionary<string, JsonElement>? fields)
    {
        if (fields is not { Count: > 0 })
        {
            return null;
        }

        var projected = new Dictionary<string, JsonElement>(fields.Count);
        foreach (var pair in fields)
        {
            switch (pair.Value.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    continue;
                case JsonValueKind.String:
                    projected[pair.Key] = JsonSerializer.SerializeToElement(
                        PromptBoundary.WrapField(pair.Value.GetString()));
                    break;
                case JsonValueKind.Array:
                    // 多值字段（#212）：逐元素包裹——每个 String 元素都是用户派生自由文本。空数组跳过。
                    var items = new List<JsonElement>();
                    foreach (var element in pair.Value.EnumerateArray())
                    {
                        switch (element.ValueKind)
                        {
                            case JsonValueKind.Null:
                            case JsonValueKind.Undefined:
                                continue;
                            case JsonValueKind.String:
                                items.Add(JsonSerializer.SerializeToElement(
                                    PromptBoundary.WrapField(element.GetString())));
                                break;
                            default:
                                items.Add(element);
                                break;
                        }
                    }
                    if (items.Count > 0)
                    {
                        projected[pair.Key] = JsonSerializer.SerializeToElement(items);
                    }
                    break;
                default:
                    projected[pair.Key] = pair.Value;
                    break;
            }
        }

        return projected.Count > 0 ? projected : null;
    }
}
