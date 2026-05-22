import { mapEnumToOptions } from '@abp/ng.core';

// Mirrors C# Dignite.Paperbase.Documents.FieldDataType (Domain.Shared).
export enum FieldDataType {
  String = 0,
  Integer = 1,
  Decimal = 2,
  Boolean = 3,
  Date = 4,
  DateTime = 5,
}

export const fieldDataTypeOptions = mapEnumToOptions(FieldDataType);
