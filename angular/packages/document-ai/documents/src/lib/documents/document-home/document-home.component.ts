import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import {
  DocumentStatisticsDto,
  DocumentStatisticsService,
  DOCUMENT_AI_PERMISSIONS,
} from '@dignite/document-ai';
import { EMPTY, Subject } from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';
import { DocumentUploadComponent } from '../document-upload/document-upload.component';
import { formatBytes } from '../../shared/format-bytes';

@Component({
  selector: 'lib-document-home',
  templateUrl: './document-home.component.html',
  styleUrls: ['./document-home.component.scss'],
  imports: [CommonModule, RouterModule, LocalizationPipe, DocumentUploadComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentHomeComponent implements OnInit {
  private readonly permissionService = inject(PermissionService);
  private readonly statisticsService = inject(DocumentStatisticsService);
  private readonly destroyRef = inject(DestroyRef);

  readonly canUpload = this.permissionService.getGrantedPolicy(
    DOCUMENT_AI_PERMISSIONS.Documents.Upload,
  );
  readonly canReview = this.permissionService.getGrantedPolicy(
    DOCUMENT_AI_PERMISSIONS.Documents.ConfirmClassification,
  );
  readonly canViewCabinets = this.permissionService.getGrantedPolicy(
    DOCUMENT_AI_PERMISSIONS.Cabinets.Default,
  );

  readonly stats = signal<DocumentStatisticsDto | null>(null);
  readonly statsLoading = signal(true);
  readonly statsError = signal(false);

  // The loading skeleton must render the same number of tiles the data grid will, otherwise non-reviewers
  // (who don't see the needs-review tile) get a 6 -> 5 layout jump when stats resolve.
  readonly skeletonSlots = this.canReview ? [0, 1, 2, 3, 4, 5] : [0, 1, 2, 3, 4];

  // In-flight = stored-but-not-started (Uploaded) + actively processing. Composing the display bucket here
  // keeps the API contract a faithful per-status projection (#333 decision: granularity in the DTO, grouping in the UI).
  readonly processingCount = computed(() => {
    const s = this.stats();
    return (s?.uploadedCount ?? 0) + (s?.processingCount ?? 0);
  });

  readonly isEmpty = computed(() => (this.stats()?.totalCount ?? 0) === 0);

  // Exposed so the template can format the storage tile.
  readonly formatBytes = formatBytes;

  // A trigger drives loads through switchMap so a slower earlier request can never overwrite a newer one
  // (e.g. rapid Refresh clicks): each emission cancels the previous in-flight GET. catchError keeps the
  // stream alive across failures so later retries still work.
  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        tap(() => {
          this.statsLoading.set(true);
          this.statsError.set(false);
        }),
        switchMap(() =>
          this.statisticsService.get().pipe(
            catchError(() => {
              this.statsError.set(true);
              this.statsLoading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(stats => {
        this.stats.set(stats);
        this.statsLoading.set(false);
      });
  }

  ngOnInit(): void {
    this.loadStatistics();
  }

  loadStatistics(): void {
    this.reload$.next();
  }
}
