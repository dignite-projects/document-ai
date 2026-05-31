using System;
using System.Security.Cryptography;

namespace Dignite.Paperbase.Documents;

/// <summary>
/// 内容指纹工具（#210 review）：SHA-256 → 小写 hex，是 Paperbase 内容哈希的<b>规范形式</b>。
/// 由上传去重（<c>FileOrigin.ContentHash</c>）与原生 payload 归档 manifest（<c>NativePayloadManifest.Sha256</c>）共用——
/// 集中一处，避免哈希算法 / 大小写约定在多个调用点静默漂移。
/// </summary>
public static class ContentHasher
{
    /// <summary>计算字节内容的 SHA-256，返回小写 hex 字符串。</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> content)
        => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
