export const DOCUMENT_AI_PERMISSIONS = {
  Documents: {
    Default: 'DocumentAI.Documents',
    Upload: 'DocumentAI.Documents.Upload',
    Delete: 'DocumentAI.Documents.Delete',
    PermanentDelete: 'DocumentAI.Documents.PermanentDelete',
    Restore: 'DocumentAI.Documents.Restore',
    Export: 'DocumentAI.Documents.Export',
    ConfirmClassification: 'DocumentAI.Documents.ConfirmClassification',
    Pipelines: {
      Default: 'DocumentAI.Documents.Pipelines',
      Retry: 'DocumentAI.Documents.Pipelines.Retry',
    },
    // Batch reprocessing of existing documents (#289) — admin-level.
    Reprocessing: {
      Default: 'DocumentAI.Documents.Reprocessing',
      FieldExtraction: 'DocumentAI.Documents.Reprocessing.FieldExtraction',
      Reclassification: 'DocumentAI.Documents.Reprocessing.Reclassification',
    },
    Templates: {
      Default: 'DocumentAI.Documents.Templates',
      Create: 'DocumentAI.Documents.Templates.Create',
      Update: 'DocumentAI.Documents.Templates.Update',
      Delete: 'DocumentAI.Documents.Templates.Delete',
    },
  },
  Cabinets: {
    Default: 'DocumentAI.Cabinets',
    Create: 'DocumentAI.Cabinets.Create',
    Update: 'DocumentAI.Cabinets.Update',
    Delete: 'DocumentAI.Cabinets.Delete',
  },
  // Document-type schema management (#217) — admin-level, independent of document CRUD.
  DocumentTypes: {
    Default: 'DocumentAI.DocumentTypes',
    Create: 'DocumentAI.DocumentTypes.Create',
    Update: 'DocumentAI.DocumentTypes.Update',
    Delete: 'DocumentAI.DocumentTypes.Delete',
  },
  // Field-definition schema management (#217) — admin-level, independent of document CRUD.
  FieldDefinitions: {
    Default: 'DocumentAI.FieldDefinitions',
    Create: 'DocumentAI.FieldDefinitions.Create',
    Update: 'DocumentAI.FieldDefinitions.Update',
    Delete: 'DocumentAI.FieldDefinitions.Delete',
  },
} as const;
