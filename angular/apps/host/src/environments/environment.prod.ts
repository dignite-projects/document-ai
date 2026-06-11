import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44348/',
  redirectUri: baseUrl,
  clientId: 'DocumentAI_App',
  responseType: 'code',
  scope: 'offline_access DocumentAI',
  requireHttps: true,
};

export const environment = {
  production: true,
  application: {
    baseUrl,
    name: 'Document AI',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44348',
      rootNamespace: 'Dignite.DocumentAI',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;
