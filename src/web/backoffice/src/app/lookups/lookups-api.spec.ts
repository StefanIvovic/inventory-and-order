import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { BACKGROUND } from '../loading/loading.interceptor';
import { LookupsApi } from './lookups-api';

describe('LookupsApi', () => {
  let http: HttpTestingController;
  let api: LookupsApi;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(LookupsApi);
  });

  afterEach(() => http.verify());

  it('reads the values of one list by its code', async () => {
    const items = [
      { id: 'a', code: 'active', name: 'Active' },
      { id: 'd', code: 'disabled', name: 'Disabled' },
    ];
    const answer = firstValueFrom(api.values('staff-status'));

    const request = http.expectOne((candidate) => candidate.url === '/api/identity/lookups/staff-status/values');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('size')).toBe('200');
    request.flush({ items, nextCursor: null });

    expect(await answer).toEqual(items);
  });

  it('asks for a page with search, ids, codes and cursor', async () => {
    const answer = firstValueFrom(api.page('a b', { q: ' sta ', ids: ['1', '2'], codes: ['x'], size: 50, after: 'c1' }));

    const request = http.expectOne((candidate) => candidate.url === '/api/identity/lookups/a%20b/values');
    const params = request.request.params;
    expect(params.get('q')).toBe('sta');
    expect(params.getAll('ids')).toEqual(['1', '2']);
    expect(params.getAll('codes')).toEqual(['x']);
    expect(params.get('size')).toBe('50');
    expect(params.get('after')).toBe('c1');
    request.flush({ items: [], nextCursor: 'c2' });

    expect(await answer).toEqual({ items: [], nextCursor: 'c2' });
  });

  it('leaves out an empty search and uses another service when asked', async () => {
    const answer = firstValueFrom(api.page('unit', { q: '  ', size: 50 }, '/api/inventory'));

    const request = http.expectOne((candidate) => candidate.url === '/api/inventory/lookups/unit/values');
    expect(request.request.params.keys()).toEqual(['size']);
    request.flush({ items: [], nextCursor: null });

    await answer;
  });

  it('marks values and pages as background only when asked', () => {
    api.values('staff-status').subscribe();
    api.values('user-type', { background: true }).subscribe();
    api.page('staff-status', { size: 50 }).subscribe();
    api.page('user-type', { size: 50 }, undefined, { background: true }).subscribe();

    const requests = http.match(() => true);
    expect(requests.map((request) => [request.request.url, request.request.context.get(BACKGROUND)])).toEqual([
      [LookupsApi.valuesUrl('staff-status'), false],
      [LookupsApi.valuesUrl('user-type'), true],
      [LookupsApi.valuesUrl('staff-status'), false],
      [LookupsApi.valuesUrl('user-type'), true],
    ]);
    requests.forEach((request) => request.flush({ items: [], nextCursor: null }));
  });
});
