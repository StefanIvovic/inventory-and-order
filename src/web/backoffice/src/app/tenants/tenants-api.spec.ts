import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { BACKGROUND } from '../loading/loading.interceptor';
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

  const list: TenantList = {
    items: [{ id: '1', slug: 'demo', name: 'Demo', createdAt: '2026-11-24T10:00:00Z' }],
    nextCursor: 'c2',
    total: 3,
    actions: { canCreate: true },
  };

  function params(url: string): Record<string, string | null> {
    const request = http.expectOne((candidate) => candidate.url === url);
    const all = request.request.params;
    const found = Object.fromEntries(all.keys().map((key) => [key, all.get(key)]));
    request.flush(list);
    return found;
  }

  it('asks only for the size when nothing else is set', async () => {
    const answer = firstValueFrom(api.list({ q: '  ', size: 25, after: null, includeTotal: false }));

    const request = http.expectOne((candidate) => candidate.url === TenantsApi.url);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.keys()).toEqual(['size']);
    expect(request.request.params.get('size')).toBe('25');
    request.flush(list);

    expect(await answer).toEqual(list);
  });

  it('sends the trimmed search, the cursor and the total request', () => {
    api.list({ q: ' Acme ', size: 10, after: 'c1', includeTotal: true }).subscribe();

    expect(params(TenantsApi.url)).toEqual({ size: '10', q: 'Acme', after: 'c1', includeTotal: 'true' });
  });

  it('marks the list as background only when asked', () => {
    const request = { q: '', size: 25, after: null, includeTotal: false };
    api.list(request).subscribe();
    api.list({ ...request, after: 'c2' }, { background: true }).subscribe();

    const [plain, quiet] = http.match((candidate) => candidate.url === TenantsApi.url);
    expect(plain.request.context.get(BACKGROUND)).toBe(false);
    expect(quiet.request.context.get(BACKGROUND)).toBe(true);
    plain.flush(list);
    quiet.flush(list);
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
