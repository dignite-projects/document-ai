using System;
using System.Security.Cryptography;
using System.Text;

namespace Dignite.Paperbase.Vectors;

// Deterministic key derivation ported verbatim from the previous
// QdrantPointIdGenerator. Same SHA1(tenant|document|chunk) algorithm produces the
// same Guid for the same input, so re-running the embedding job on the same source
// chunk is an idempotent upsert without server-side dedupe logic.
public static class DocumentChunkPointId
{
    public static Guid Create(Guid? tenantId, Guid documentId, int chunkIndex)
    {
        var key = string.Join(
            "|",
            DocumentChunkPayloadEncoding.EncodeTenantId(tenantId),
            DocumentChunkPayloadEncoding.EncodeDocumentId(documentId),
            chunkIndex.ToString());

        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(key));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);

        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
