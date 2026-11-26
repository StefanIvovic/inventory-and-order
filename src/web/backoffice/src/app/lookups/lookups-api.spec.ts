import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
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
});
