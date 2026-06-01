using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Values;

namespace Dignite.Paperbase.Documents;

/// <summary>
/// 原生 payload 归档清单（持久化值对象，#210）——provider 原生输出已写入 blob，本类只存<b>取回它所需的元数据</b>，
/// 原始字节<b>不进 DB</b>。作为 <see cref="DocumentTextExtractionMetadata.NativePayloadManifest"/> 的成员整体序列化进
/// <c>Document.ExtractionMetadata</c> 的 JSON 列。
/// <para>
/// get-only 属性 + 唯一带参构造让 System.Text.Json 反序列化复用同一构造（参数名匹配属性名），套路同 <c>ExportColumn</c>。
/// <b>无</b> archive 时 <see cref="DocumentTextExtractionMetadata.NativePayloadManifest"/> 整体为 <c>null</c>（不构造空壳）。
/// </para>
/// </summary>
public class NativePayloadManifest : ValueObject
{
    /// <summary>
    /// 归档 blob 的稳定 per-document key（<c>extraction-native/{documentId}</c>，重提取覆盖）。
    /// <b>内部存储 key</b>——<b>绝不</b>暴露给出口 DTO（无下载端点前是存储 key 泄漏）；永久删除时按本 key 删归档 blob。
    /// </summary>
    public string BlobName { get; }

    /// <summary>归档 payload 的 MIME 类型（如 <c>application/json</c>）。</summary>
    public string ContentType { get; }

    /// <summary>归档 payload 的字节大小。</summary>
    public long SizeBytes { get; }

    /// <summary>归档 payload 的 SHA-256（小写 hex），供完整性校验 / 去重。</summary>
    public string Sha256 { get; }

    /// <summary>payload schema 标识（如 <c>PaddleOCR/PP-StructureV3</c>），供消费方判断如何解析。</summary>
    public string SchemaName { get; }

    public NativePayloadManifest(string blobName, string contentType, long sizeBytes, string sha256, string schemaName)
    {
        BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
        ContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Sha256 = Check.NotNullOrWhiteSpace(sha256, nameof(sha256));
        SchemaName = Check.NotNullOrWhiteSpace(schemaName, nameof(schemaName));
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return BlobName;
        yield return ContentType;
        yield return SizeBytes;
        yield return Sha256;
        yield return SchemaName;
    }
}
