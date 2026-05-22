import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import {
  CreateFieldDefinitionDto,
  FieldDataType,
  FieldDefinitionDto,
  FieldDefinitionService,
  fieldDataTypeOptions,
  PAPERBASE_PERMISSIONS,
} from '@dignite/paperbase';

// Mirrors FieldDefinitionConsts (Domain.Shared): Name whitelist + length caps.
const NAME_PATTERN = /^[A-Za-z0-9_\-]{1,64}$/;
const MAX_NAME_LENGTH = 64;
const MAX_DISPLAY_NAME_LENGTH = 128;
const MAX_PROMPT_LENGTH = 1024;

@Component({
  selector: 'lib-field-definition-list',
  templateUrl: './field-definition-list.component.html',
  styleUrls: ['./field-definition-list.component.scss'],
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FieldDefinitionListComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(FieldDefinitionService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly canManage = this.permissionService.getGrantedPolicy(
    PAPERBASE_PERMISSIONS.Documents.ConfirmClassification,
  );
  readonly dataTypeOptions = fieldDataTypeOptions;
  readonly FieldDataType = FieldDataType;

  documentTypeCode = '';
  fields = signal<FieldDefinitionDto[]>([]);
  isLoading = signal(true);
  showDeleted = signal(false);

  editing = signal<FieldDefinitionDto | 'create' | null>(null);
  isSubmitting = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: [
      '',
      [Validators.required, Validators.maxLength(MAX_NAME_LENGTH), Validators.pattern(NAME_PATTERN)],
    ],
    displayName: ['', [Validators.required, Validators.maxLength(MAX_DISPLAY_NAME_LENGTH)]],
    prompt: ['', [Validators.required, Validators.maxLength(MAX_PROMPT_LENGTH)]],
    dataType: [FieldDataType.String, [Validators.required]],
    displayOrder: [0, [Validators.required]],
    isRequired: [false],
  });

  ngOnInit(): void {
    this.documentTypeCode = this.route.snapshot.paramMap.get('typeCode') ?? '';
    this.load();
  }

  refresh(): void {
    this.load();
  }

  toggleDeleted(): void {
    this.showDeleted.update(v => !v);
    this.load();
  }

  goBack(): void {
    this.router.navigate(['/documents/types']);
  }

  private load(): void {
    this.isLoading.set(true);
    const source$ = this.showDeleted()
      ? this.service.getDeletedByDocumentType(this.documentTypeCode)
      : this.service.getByDocumentType(this.documentTypeCode);
    source$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: list => {
        this.fields.set([...list].sort((a, b) => a.displayOrder - b.displayOrder));
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  openCreate(): void {
    const nextOrder = this.fields().reduce((max, f) => Math.max(max, f.displayOrder), -1) + 1;
    this.form.reset({
      name: '',
      displayName: '',
      prompt: '',
      dataType: FieldDataType.String,
      displayOrder: nextOrder,
      isRequired: false,
    });
    this.form.controls.name.enable();
    this.editing.set('create');
  }

  openEdit(field: FieldDefinitionDto): void {
    this.form.reset({
      name: field.name,
      displayName: field.displayName,
      prompt: field.prompt,
      dataType: field.dataType,
      displayOrder: field.displayOrder,
      isRequired: field.isRequired,
    });
    // Name is immutable after creation (UpdateFieldDefinitionDto omits it).
    this.form.controls.name.disable();
    this.editing.set(field);
  }

  closeModal(): void {
    this.editing.set(null);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const mode = this.editing();
    if (mode === null) return;

    this.isSubmitting.set(true);
    const raw = this.form.getRawValue();

    if (mode === 'create') {
      const input: CreateFieldDefinitionDto = {
        documentTypeCode: this.documentTypeCode,
        name: raw.name,
        displayName: raw.displayName,
        prompt: raw.prompt,
        dataType: raw.dataType,
        displayOrder: raw.displayOrder,
        isRequired: raw.isRequired,
      };
      this.service.create(input)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => this.onSaved('::FieldDefinition:CreatedSuccessfully'),
          error: () => this.isSubmitting.set(false),
        });
    } else {
      this.service.update(mode.id, {
        displayName: raw.displayName,
        prompt: raw.prompt,
        dataType: raw.dataType,
        displayOrder: raw.displayOrder,
        isRequired: raw.isRequired,
      })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: () => this.onSaved('::FieldDefinition:UpdatedSuccessfully'),
          error: () => this.isSubmitting.set(false),
        });
    }
  }

  private onSaved(messageKey: string): void {
    this.isSubmitting.set(false);
    this.closeModal();
    this.toaster.success(messageKey, '::Success');
    this.load();
  }

  delete(field: FieldDefinitionDto): void {
    this.confirmation
      .warn('::FieldDefinition:AreYouSureToDelete', '::AreYouSure')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(status => {
        if (status !== Confirmation.Status.confirm) return;
        this.service.delete(field.id)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe({
            next: () => {
              this.toaster.success('::FieldDefinition:DeletedSuccessfully', '::Success');
              this.load();
            },
          });
      });
  }

  restore(field: FieldDefinitionDto): void {
    this.service.restore(field.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toaster.success('::FieldDefinition:RestoredSuccessfully', '::Success');
          this.load();
        },
      });
  }

  dataTypeLabel(dataType: FieldDataType): string {
    return this.dataTypeOptions.find(o => o.value === dataType)?.key ?? String(dataType);
  }
}
