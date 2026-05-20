using System.Collections.Generic;

namespace Dignite.Paperbase.Abstractions.TextExtraction;

public class TextExtractionContext
{
    /// <summary>MIME 类型，由核心模块从 Document.FileOrigin.ContentType 传入。</summary>
    public string ContentType { get; set; } = default!;

    /// <summary>文件扩展名（含点号，如 ".pdf"）。</summary>
    public string FileExtension { get; set; } = default!;

    /// <summary>语言提示（BCP 47 列表，如 ["ja", "en"]）。</summary>
    public IList<string> LanguageHints { get; set; } = new List<string>();

    /// <summary>
    /// Requested provider-neutral OCR profile. Null means the host/default OCR options decide;
    /// normal uploads default to <c>auto</c>.
    /// </summary>
    public string? OcrProfileCode { get; set; }
}
