using System;

namespace Dignite.Paperbase.Vectors;

// Schema-and-encoding conventions for DocumentChunkRecord. Kept separate from the
// POCO so the record stays pure shape (attributes + properties), while the rules
// that govern its on-disk representation live in one place — and can be referenced
// at consumer call sites without dragging the whole record type into scope just to
// call an encoder.
//
// Storage names + the HostTenantId sentinel are part of the persistence contract:
// any value already written to Qdrant carries the literal "__host__" or
// "tenant_id"/"document_id"/etc. payload keys. Renaming them post-deployment
// silently orphans the existing data — see comments on individual members.
public static class DocumentChunkPayloadEncoding
{
    // SENTINEL — do not change once any data has been written. This is the literal
    // string persisted into the tenant_id payload field for host-level documents
    // (Document.TenantId == null). Renaming the constant would orphan every
    // host-level chunk in production: the tenant filter at query time would use the
    // new value while existing data still carries the old literal.
    public const string HostTenantId = "__host__";

    // Storage names — these are the actual Qdrant payload field names. Don't rename
    // post-deployment for the same reason as HostTenantId.
    public const string TenantIdStorageName = "tenant_id";
    public const string DocumentIdStorageName = "document_id";
    public const string DocumentTypeCodeStorageName = "document_type_code";
    public const string ChunkIndexStorageName = "chunk_index";
    public const string TextStorageName = "text";
    public const string PageNumberStorageName = "page_number";

    public static string EncodeTenantId(Guid? tenantId)
        => tenantId?.ToString("D") ?? HostTenantId;

    public static string EncodeDocumentId(Guid documentId)
        => documentId.ToString("D");
}
