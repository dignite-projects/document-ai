namespace Dignite.Paperbase.Abstractions.TextExtraction;

public class TextExtractionResult
{
    /// <summary>
    /// 结构化 Markdown 输出。Provider 未识别到任何内容时为空字符串。
    /// 这是 TextExtraction 流水线的<b>唯一</b>文本载荷——下游需要纯文本时通过
    /// <see cref="Dignite.Paperbase.Documents.MarkdownStripper"/> 投影。
    /// </summary>
    public string Markdown { get; set; } = string.Empty;

    public string? DetectedLanguage { get; set; }
    public int PageCount { get; set; }

    /// <summary>true = OCR (physical scan), false = direct text layer (digital)</summary>
    public bool UsedOcr { get; set; }

    /// <summary>胜出 provider 的家族 / 名称（最终产出 Markdown 的那个 provider；可空——历史 / 未知时 null）。</summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// 胜出 provider 的<b>原生输出 payload</b>（空间信号原料，#210）；无则 <c>null</c>。
    /// 由文本提取 job 归档进 blob——<b>不进 DB</b>、<b>不并列暴露为文本字段</b>。
    /// </summary>
    public NativePayload? NativePayload { get; set; }
}
