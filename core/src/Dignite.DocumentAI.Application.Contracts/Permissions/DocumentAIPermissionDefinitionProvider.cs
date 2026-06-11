using Dignite.DocumentAI.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.DocumentAI.Permissions;

public class DocumentAIPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(DocumentAIPermissions.GroupName, L("Permission:DocumentAI"));

        var documents = group.AddPermission(DocumentAIPermissions.Documents.Default, L("Permission:Documents"));
        documents.AddChild(DocumentAIPermissions.Documents.Upload, L("Permission:Documents.Upload"));
        documents.AddChild(DocumentAIPermissions.Documents.Delete, L("Permission:Documents.Delete"));
        documents.AddChild(DocumentAIPermissions.Documents.PermanentDelete, L("Permission:Documents.PermanentDelete"));
        documents.AddChild(DocumentAIPermissions.Documents.Restore, L("Permission:Documents.Restore"));
        documents.AddChild(DocumentAIPermissions.Documents.Export, L("Permission:Documents.Export"));
        documents.AddChild(DocumentAIPermissions.Documents.ConfirmClassification, L("Permission:Documents.ConfirmClassification"));

        var pipelines = documents.AddChild(DocumentAIPermissions.Documents.Pipelines.Default, L("Permission:Documents.Pipelines"));
        pipelines.AddChild(DocumentAIPermissions.Documents.Pipelines.Retry, L("Permission:Documents.Pipelines.Retry"));

        var reprocessing = documents.AddChild(DocumentAIPermissions.Documents.Reprocessing.Default, L("Permission:Documents.Reprocessing"));
        reprocessing.AddChild(DocumentAIPermissions.Documents.Reprocessing.FieldExtraction, L("Permission:Documents.Reprocessing.FieldExtraction"));
        reprocessing.AddChild(DocumentAIPermissions.Documents.Reprocessing.Reclassification, L("Permission:Documents.Reprocessing.Reclassification"));

        var templates = documents.AddChild(DocumentAIPermissions.Documents.Templates.Default, L("Permission:Documents.Templates"));
        templates.AddChild(DocumentAIPermissions.Documents.Templates.Create, L("Permission:Documents.Templates.Create"));
        templates.AddChild(DocumentAIPermissions.Documents.Templates.Update, L("Permission:Documents.Templates.Update"));
        templates.AddChild(DocumentAIPermissions.Documents.Templates.Delete, L("Permission:Documents.Templates.Delete"));

        var cabinets = group.AddPermission(DocumentAIPermissions.Cabinets.Default, L("Permission:Cabinets"));
        cabinets.AddChild(DocumentAIPermissions.Cabinets.Create, L("Permission:Cabinets.Create"));
        cabinets.AddChild(DocumentAIPermissions.Cabinets.Update, L("Permission:Cabinets.Update"));
        cabinets.AddChild(DocumentAIPermissions.Cabinets.Delete, L("Permission:Cabinets.Delete"));

        var documentTypes = group.AddPermission(DocumentAIPermissions.DocumentTypes.Default, L("Permission:DocumentTypes"));
        documentTypes.AddChild(DocumentAIPermissions.DocumentTypes.Create, L("Permission:DocumentTypes.Create"));
        documentTypes.AddChild(DocumentAIPermissions.DocumentTypes.Update, L("Permission:DocumentTypes.Update"));
        documentTypes.AddChild(DocumentAIPermissions.DocumentTypes.Delete, L("Permission:DocumentTypes.Delete"));

        var fieldDefinitions = group.AddPermission(DocumentAIPermissions.FieldDefinitions.Default, L("Permission:FieldDefinitions"));
        fieldDefinitions.AddChild(DocumentAIPermissions.FieldDefinitions.Create, L("Permission:FieldDefinitions.Create"));
        fieldDefinitions.AddChild(DocumentAIPermissions.FieldDefinitions.Update, L("Permission:FieldDefinitions.Update"));
        fieldDefinitions.AddChild(DocumentAIPermissions.FieldDefinitions.Delete, L("Permission:FieldDefinitions.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DocumentAIResource>(name);
    }
}
