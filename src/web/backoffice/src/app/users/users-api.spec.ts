import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { StaffList, UsersApi } from './users-api';

describe('UsersApi', () => {
  let http: HttpTestingController;
  let api: UsersApi;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(UsersApi);
  });

  afterEach(() => http.verify());

  const empty: StaffList = { items: [], nextCursor: null, actions: { canCreate: false } };

  it('asks only for the size when no filter is set', async () => {
    const answer = firstValueFrom(api.list({ q: '  ', status: null, role: null, size: 25, after: null, includeTotal: false }));

    const request = http.expectOne((candidate) => candidate.url === UsersApi.url);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.keys()).toEqual(['size']);
    expect(request.request.params.get('size')).toBe('25');
    request.flush(empty);

    expect(await answer).toEqual(empty);
  });

  it('sends every filter, the cursor and the total request', async () => {
    const answer = firstValueFrom(
      api.list({ q: ' ann ', status: 'disabled', role: 'identity.viewer', size: 10, after: 'abc', includeTotal: true }),
    );

    const request = http.expectOne((candidate) => candidate.url === UsersApi.url);
    expect(Object.fromEntries(request.request.params.keys().map((key) => [key, request.request.params.get(key)]))).toEqual({
      size: '10',
      q: 'ann',
      status: 'disabled',
      role: 'identity.viewer',
      after: 'abc',
      includeTotal: 'true',
    });
    request.flush({ ...empty, total: 0 });

    expect((await answer).total).toBe(0);
  });

  it('creates a staff user', async () => {
    const answer = firstValueFrom(api.create({ email: 'clerk@demo.test', password: 'Secret#123' }));

    const request = http.expectOne(UsersApi.url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'clerk@demo.test', password: 'Secret#123' });
    request.flush({ id: 'u-9' }, { status: 201, statusText: 'Created' });

    expect(await answer).toEqual({ id: 'u-9' });
  });

  it('disables a user', async () => {
    const answer = firstValueFrom(api.disable('u-9'));

    const request = http.expectOne('/api/identity/users/u-9/disable');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await answer;
  });

  it('assigns and removes a role with the role name encoded in the path', async () => {
    const assigned = firstValueFrom(api.assign('u-9', 'identity.viewer'));
    const assign = http.expectOne('/api/identity/users/u-9/roles/identity.viewer');
    expect(assign.request.method).toBe('PUT');
    assign.flush(null, { status: 204, statusText: 'No Content' });
    await assigned;

    const removed = firstValueFrom(api.remove('u-9', 'a/b c'));
    const remove = http.expectOne('/api/identity/users/u-9/roles/a%2Fb%20c');
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null, { status: 204, statusText: 'No Content' });
    await removed;
  });
});
