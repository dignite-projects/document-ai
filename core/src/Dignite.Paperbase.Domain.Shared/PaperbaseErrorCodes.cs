namespace Dignite.Paperbase;

public static class PaperbaseErrorCodes
{
    public const string MarkdownIsImmutable = "Paperbase:MarkdownIsImmutable";
    public const string TitleIsImmutable = "Paperbase:TitleIsImmutable";
    public const string InvalidDocumentTypeCode = "Paperbase:InvalidDocumentTypeCode";
    public const string DocumentTypeCodeAlreadyExists = "Paperbase:DocumentTypeCodeAlreadyExists";
    public const string DocumentTypeInUse = "Paperbase:DocumentTypeInUse";
    public const string DocumentTypeRestoreConflict = "Paperbase:DocumentTypeRestoreConflict";
    public const string FieldDefinitionAlreadyExists = "Paperbase:FieldDefinitionAlreadyExists";
    public const string FieldDefinitionRestoreConflict = "Paperbase:FieldDefinitionRestoreConflict";
    public const string FieldDefinitionParentTypeMissing = "Paperbase:FieldDefinitionParentTypeMissing";
    public const string DocumentDuplicate = "Paperbase:DocumentDuplicate";
    public const string DocumentInRecycleBin = "Paperbase:DocumentInRecycleBin";
    public const string PipelineNotRetryable = "Paperbase:PipelineNotRetryable";
    public const string PipelineRetryInProgress = "Paperbase:PipelineRetryInProgress";
    public const string PipelineNeverRan = "Paperbase:PipelineNeverRan";
    public const string UnknownPipelineCode = "Paperbase:UnknownPipelineCode";
}
