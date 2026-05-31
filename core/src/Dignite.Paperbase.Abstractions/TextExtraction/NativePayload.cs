namespace Dignite.Paperbase.Abstractions.TextExtraction;

/// <summary>
/// 文本提取 provider 的<b>原生输出 payload</b>（与 <see cref="TextExtractionResult.Markdown"/> 文本载荷
/// 正交的 out-of-band 空间信号原料：bbox / 表格 cell / text-span 锚点 / 置信度 / 区域类型等）。
/// <para>
/// <b>通用 provider 能力</b>（非 OCR 路径独有）：OCR 经 <c>OcrResult</c> 映射上来；富 Markdown provider
/// （未来 Docling / liteparse 类）可直填；纯 text→Markdown 的 provider（ElBruno / MarkItDown）无空间模型，留 <c>null</c>。
/// 由文本提取 job 归档进 blob（<b>不进 DB</b>、<b>不塞回 Markdown 字符串</b>、<b>不在契约上并列暴露文本字段</b>）。
/// </para>
/// </summary>
public sealed class NativePayload
{
    /// <summary>原生输出的不透明字节（通常是 provider 原始响应的 UTF-8 编码 JSON）。</summary>
    public byte[] Content { get; }

    /// <summary>payload 的 MIME 类型（如 <c>application/json</c>）。</summary>
    public string ContentType { get; }

    /// <summary>schema 标识，供下游消费方判断如何解析（如 <c>PaddleOCR/PP-StructureV3</c> / <c>AzureDocumentIntelligence.AnalyzeResult</c>）。</summary>
    public string SchemaName { get; }

    public NativePayload(byte[] content, string contentType, string schemaName)
    {
        Content = content;
        ContentType = contentType;
        SchemaName = schemaName;
    }
}
