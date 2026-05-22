export const PAPERBASE_PERMISSIONS = {
  Documents: {
    Default: 'Paperbase.Documents',
    Upload: 'Paperbase.Documents.Upload',
    Delete: 'Paperbase.Documents.Delete',
    PermanentDelete: 'Paperbase.Documents.PermanentDelete',
    Restore: 'Paperbase.Documents.Restore',
    Export: 'Paperbase.Documents.Export',
    ConfirmClassification: 'Paperbase.Documents.ConfirmClassification',
    Pipelines: {
      Default: 'Paperbase.Documents.Pipelines',
      Retry: 'Paperbase.Documents.Pipelines.Retry',
    },
  },
} as const;
