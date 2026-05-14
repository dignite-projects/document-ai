import { eLayoutType, RoutesService } from '@abp/ng.core';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { CONTRACTS_PERMISSIONS } from '@dignite/paperbase.contracts';

export function provideContracts(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() => {
      const routes = inject(RoutesService);
      routes.add([
        {
          path: '/contracts',
          name: 'Contracts::Menu:Contracts',
          iconClass: 'fas fa-file-contract',
          requiredPolicy: CONTRACTS_PERMISSIONS.Contracts.Default,
          order: 4,
          layout: eLayoutType.application,
        },
      ]);
    }),
  ]);
}
