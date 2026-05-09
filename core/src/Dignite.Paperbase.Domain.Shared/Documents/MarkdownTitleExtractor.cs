using System;
using System.Text.RegularExpressions;

namespace Dignite.Paperbase.Documents;

/// <summary>
/// 从 Markdown 中提取一个简洁的展示标题。
/// 优先级：第一个 ATX 标题（# / ## / ...）→ 第一段非空纯文本；
/// 全部失败则返回 null，调用方负责降级到文件名等确定性回退。
/// </summary>
public static class MarkdownTitleExtractor
{
    /// <summary>
    /// 抽取展示标题。<paramref name="maxLength"/> 默认使用 <see cref="DocumentConsts.MaxTitleLength"/>。
    /// 返回值已 trim、规范化空白，长度不会超过 <paramref name="maxLength"/>；找不到任何可用文本时返回 null。
    /// </summary>
    public static string? ExtractTitle(string? markdown, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        var limit = maxLength ?? DocumentConsts.MaxTitleLength;
        if (limit <= 0)
        {
            return null;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        // 1. 优先 ATX 标题（# H1 ~ ###### H6）：取第一个出现的标题，避免漏掉前面没有 H1 但用了 H2/H3 的文档。
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var match = Regex.Match(line, @"^#{1,6}\s+(.+?)\s*#*\s*$");
            if (match.Success)
            {
                var headingText = StripInlineMarkdown(match.Groups[1].Value);
                var normalized = Normalize(headingText, limit);
                if (!string.IsNullOrEmpty(normalized))
                {
                    return normalized;
                }
            }
        }

        // 2. 没有标题就退回第一段非空文本（跳过围栏代码块、表格分隔符、列表项符号等噪声）。
        var inFence = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || line.Length == 0)
            {
                continue;
            }

            // 跳过表格分隔行 |---|---|
            if (Regex.IsMatch(line, @"^\|?[\s:\-\|]+\|?$"))
            {
                continue;
            }

            // 跳过水平线 ---, ***, ___
            if (Regex.IsMatch(line, @"^([-*_]\s*){3,}$"))
            {
                continue;
            }

            // 去掉前缀（引用 >、列表项 -/*/+ 或 1.）
            var stripped = Regex.Replace(line, @"^>\s*", string.Empty);
            stripped = Regex.Replace(stripped, @"^([-*+]|\d+\.)\s+", string.Empty);

            var candidate = StripInlineMarkdown(stripped);
            var normalized = Normalize(candidate, limit);
            if (!string.IsNullOrEmpty(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string StripInlineMarkdown(string s)
    {
        // 图片 ![alt](url) → alt
        s = Regex.Replace(s, @"!\[([^\]]*)\]\([^\)]*\)", "$1");
        // 链接 [text](url) → text
        s = Regex.Replace(s, @"\[([^\]]+)\]\([^\)]*\)", "$1");
        // 加粗/斜体
        s = Regex.Replace(s, @"(\*\*|__)(.+?)\1", "$2");
        s = Regex.Replace(s, @"(?<!\w)([*_])(.+?)\1(?!\w)", "$2");
        // 行内代码
        s = Regex.Replace(s, @"`([^`]+)`", "$1");
        return s;
    }

    private static string Normalize(string text, int limit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // 多空白折叠成单空格
        var collapsed = Regex.Replace(text, @"\s+", " ").Trim();
        if (collapsed.Length == 0)
        {
            return string.Empty;
        }

        return collapsed.Length <= limit ? collapsed : collapsed[..limit];
    }
}
