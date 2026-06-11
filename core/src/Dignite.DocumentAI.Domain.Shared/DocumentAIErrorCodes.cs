namespace Dignite.DocumentAI;

/// <summary>
/// 错误码字符串：是 i18n 字典匹配 key + 下游 consumer 按 code 分支处理的 wire-level 协议契约。
/// 必须是 <c>const</c>——任何运行时改动都会让 Localization/DocumentAI/*.json 的映射失效，
/// 并破坏下游消费方既有的 try/catch (code == "DocumentAI:Xxx") 分支逻辑。
/// 嵌套静态类仅按聚合分"抽屉"（文件柜式分组）——C# 标识符路径可调整，但**字符串值是冻结契约**，
/// 与 Localization/DocumentAI/*.json 的 key 一一对应，不得改动。
/// </summary>
public static class DocumentAIErrorCodes
{
    public static class Document
    {
        public const string MarkdownIsImmutable = "DocumentAI:MarkdownIsImmutable";
        public const string TitleIsImmutable = "DocumentAI:TitleIsImmutable";
        public const string Duplicate = "DocumentAI:DocumentDuplicate";
        public const string InRecycleBin = "DocumentAI:DocumentInRecycleBin";
        public const string NotClassified = "DocumentAI:DocumentNotClassified";
        // #263：「重新识别」（重跑自动分类）的前置——自动分类输入是 Document.Markdown，文本提取尚未产出文本则无从重判。
        public const string NotTextExtracted = "DocumentAI:DocumentNotTextExtracted";
        // #221：上传 fail-closed 校验失败码（大小超限 / content-type + 扩展名不在白名单）。
        public const string FileTooLarge = "DocumentAI:DocumentFileTooLarge";
        public const string UnsupportedFileType = "DocumentAI:DocumentUnsupportedFileType";
    }

    public static class DocumentType
    {
        public const string InvalidCodeFormat = "DocumentAI:InvalidDocumentTypeCodeFormat";
        public const string CodeAlreadyExists = "DocumentAI:DocumentTypeCodeAlreadyExists";
        public const string InUse = "DocumentAI:DocumentTypeInUse";
        public const string RestoreConflict = "DocumentAI:DocumentTypeRestoreConflict";
        public const string InvalidDisplayName = "DocumentAI:InvalidDocumentTypeDisplayName";
        public const string InvalidDescription = "DocumentAI:InvalidDocumentTypeDescription";
        public const string NoneConfigured = "DocumentAI:NoDocumentTypesConfigured";
    }

    public static class FieldDefinition
    {
        public const string AlreadyExists = "DocumentAI:FieldDefinitionAlreadyExists";
        public const string InvalidName = "DocumentAI:InvalidFieldDefinitionName";
        public const string InvalidDisplayName = "DocumentAI:InvalidFieldDefinitionDisplayName";
        public const string RestoreConflict = "DocumentAI:FieldDefinitionRestoreConflict";
        public const string ParentTypeMissing = "DocumentAI:FieldDefinitionParentTypeMissing";
        public const string DataTypeChangeNotAllowed = "DocumentAI:FieldDefinitionDataTypeChangeNotAllowed";
        public const string MultiValueRequiresStringType = "DocumentAI:FieldDefinitionMultiValueRequiresStringType";
        public const string MultiValueChangeNotAllowed = "DocumentAI:FieldDefinitionMultiValueChangeNotAllowed";
    }

    public static class ExtractedField
    {
        public const string Unknown = "DocumentAI:UnknownExtractedField";
        public const string InvalidValue = "DocumentAI:InvalidExtractedFieldValue";
        public const string FieldTypeDoesNotSupportRange = "DocumentAI:FieldTypeDoesNotSupportRange";
        public const string FieldTypeNotQueryable = "DocumentAI:FieldTypeNotQueryable";
    }

    public static class Pipeline
    {
        public const string NotRetryable = "DocumentAI:PipelineNotRetryable";
        public const string RetryInProgress = "DocumentAI:PipelineRetryInProgress";
        public const string NeverRan = "DocumentAI:PipelineNeverRan";
        public const string UnknownCode = "DocumentAI:UnknownPipelineCode";
    }

    public static class Export
    {
        public const string InvalidTemplateName = "DocumentAI:InvalidExportTemplateName";
        public const string TemplateNameAlreadyExists = "DocumentAI:ExportTemplateNameAlreadyExists";
        public const string TemplateRequiresColumn = "DocumentAI:ExportTemplateRequiresColumn";
        public const string TemplateTooManyColumns = "DocumentAI:ExportTemplateTooManyColumns";
        public const string TemplateDuplicateField = "DocumentAI:ExportTemplateDuplicateField";
        public const string DocumentLimitExceeded = "DocumentAI:ExportDocumentLimitExceeded";
    }

    // 文件柜（#194）
    public static class Cabinet
    {
        public const string InvalidName = "DocumentAI:InvalidCabinetName";
        public const string InvalidDescription = "DocumentAI:InvalidCabinetDescription";
        public const string NameAlreadyExists = "DocumentAI:CabinetNameAlreadyExists";
        public const string InvalidId = "DocumentAI:InvalidCabinetId";
    }
}
