import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';
import type { DocumentPipelineRunDto } from '../../documents/models';

/**
 * Surfaces DocumentPipelineRun (now an independent aggregate root — #216) to the UI.
 * Previously these came as DocumentDto.pipelineRuns; the field has been removed from the
 * Document aggregate read model since pipeline runs are orchestration state, not document truth.
 */
@Injectable({ providedIn: 'root' })
export class DocumentPipelineRunService {
  private readonly rest = inject(RestService);
  private readonly apiName = 'Default';
  private readonly basePath = '/api/paperbase/document-pipeline-runs';

  /**
   * Lists all runs for a given document, ordered by (PipelineCode, AttemptNumber).
   * Authorization: Documents.Default. Tenant isolation via ambient IMultiTenant filter.
   * Cross-tenant or missing document → 404 (EntityNotFoundException).
   * Route: ABP Auto API Controllers convention from GetListAsync(Guid documentId) →
   * single primitive Guid parameter ⇒ query string (matches IDocumentRelationAppService pattern).
   */
  getListByDocument = (documentId: string): Observable<DocumentPipelineRunDto[]> =>
    this.rest.request<void, DocumentPipelineRunDto[]>(
      { method: 'GET', url: this.basePath, params: { documentId } },
      { apiName: this.apiName }
    );
}
