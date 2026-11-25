import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { TenantList, TenantsApi } from './tenants-api';

describe('TenantsApi', () => {
  let http: HttpTestingController;
  let api: TenantsApi;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(TenantsApi);
  });

  afterEach(() => http.verify());

  it('lists tenants', async () => {
    const list: TenantList = { items: [{ id: '1', slug: 'demo', name: 'Demo', createdAt: '2026-11-24T10:00:00Z' }], actions: { canCreate: true } };
    const answer = firstValueFrom(api.list());

    const request = http.expectOne('/api/identity/tenants');
    expect(request.request.method).toBe('GET');
    request.flush(list);

    expect(await answer).toEqual(list);
  });

  it('creates a tenant', async () => {
    const body = { slug: 'acme', name: 'Acme', adminEmail: 'admin@acme.test', adminPassword: 'Secret#123' };
    const answer = firstValueFrom(api.create(body));

    const request = http.expectOne('/api/identity/tenants');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({ id: 't-2', slug: 'acme', adminUserId: 'u-2' }, { status: 201, statusText: 'Created' });

    expect(await answer).toEqual({ id: 't-2', slug: 'acme', adminUserId: 'u-2' });
  });
});
