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

    /// <summary>Restrict search to a document type. Ignored when <see cref="DocumentId"/> is set.</summary>
    public string? DocumentTypeCode { get; init; }

    /// <summary>Override <see cref="PaperbaseVectorStoreOptions.DefaultTopK"/>.</summary>
    public int? TopK { get; init; }

    /// <summary>Override <see cref="PaperbaseVectorStoreOptions.MinScore"/>.</summary>
    public double? MinScore { get; init; }

    /// <summary>
    /// Restrict search to a set of documents.
    /// When non-null and non-empty this supersedes <see cref="DocumentId"/>.
    /// Passed through to the vector search filter as a Qdrant payload <c>IN</c> match.
    /// </summary>
    public IReadOnlyList<Guid>? DocumentIds { get; init; }
}
