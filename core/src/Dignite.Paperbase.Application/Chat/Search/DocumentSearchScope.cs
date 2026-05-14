using System;
using System.Collections.Generic;
using Dignite.Paperbase.Vectors;

namespace Dignite.Paperbase.Chat.Search;

/// <summary>
/// Optional per-call overrides for <see cref="DocumentTextSearchAdapter"/>. Anything
/// left null falls back to <see cref="PaperbaseVectorStoreOptions"/> defaults so callers can
/// scope an Agent Framework search to a single document, document type, or change
/// the retrieval mode without rebuilding the adapter wiring.
/// </summary>
public sealed class DocumentSearchScope
{
    /// <summary>Restrict search to a single document. Null means all documents.</summary>
    public Guid? DocumentId { get; init; }

    /// <summary>Restrict search to a document type. Combined with <see cref="DocumentId"/> /
    /// <see cref="DocumentIds"/> via AND when those are also set.</summary>
    public string? DocumentTypeCode { get; init; }

    /// <summary>Override <see cref="PaperbaseVectorStoreOptions.DefaultTopK"/>.</summary>
    public int? TopK { get; init; }

    /// <summary>Override <see cref="PaperbaseVectorStoreOptions.MinScore"/>.</summary>
    public double? MinScore { get; init; }

    /// <summary>
    /// Restrict search to a set of documents.
    /// When non-null and non-empty this supersedes <see cref="DocumentId"/>.
    /// Translated to an OR chain (<c>DocumentId == k0 || DocumentId == k1 || …</c>)
    /// inside the Qdrant filter, because Qdrant's LINQ translator does not support
    /// <c>string[].Contains()</c> (MethodCallExpression).
    /// </summary>
    public IReadOnlyList<Guid>? DocumentIds { get; init; }
}
