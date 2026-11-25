import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { SessionHarness, loadAs, setUpSession, staffUser } from './testing/session-harness';

describe('apiInterceptor', () => {
  let harness: SessionHarness;
  let client: HttpClient;

  beforeEach(async () => {
    harness = setUpSession();
    client = TestBed.inject(HttpClient);
    await loadAs(harness, staffUser);
  });

  afterEach(() => harness.http.verify());

  it('adds the app header to API calls only', () => {
    client.get('/api/identity/users').subscribe();
    client.get('/backoffice/x.json').subscribe();
    client.get('/bff/backoffice/user').subscribe();

    expect(harness.http.expectOne('/api/identity/users').request.headers.get('X-Bff-App')).toBe('backoffice');
    expect(harness.http.expectOne('/backoffice/x.json').request.headers.has('X-Bff-App')).toBe(false);
    expect(harness.http.expectOne('/bff/backoffice/user').request.headers.has('X-Bff-App')).toBe(false);
  });

  it('adds nothing but the app header', () => {
    client.get('/api/identity/users').subscribe();

    const headers = harness.http.expectOne('/api/identity/users').request.headers;
    expect(headers.keys()).toEqual(['X-Bff-App']);
  });

  it.each(['session-required', 'session-ended'])('a 401 %s signs the user out and reaches the caller', (type) => {
    let failure: unknown;
    client.get('/api/identity/users').subscribe({ error: (error: unknown) => (failure = error) });

    harness.http.expectOne('/api/identity/users').flush({ type }, { status: 401, statusText: 'Unauthorized' });

    expect(harness.session.state()).toEqual({ kind: 'signed-out', error: null });
    expect((failure as HttpErrorResponse).status).toBe(401);
  });

  it('a 503 identity-unavailable waits for retry and then sends the request again', () => {
    let answer: unknown;
    let failed = false;
    client.get('/api/identity/users').subscribe({ next: (value) => (answer = value), error: () => (failed = true) });

    harness.http.expectOne('/api/identity/users').flush({ type: 'identity-unavailable' }, { status: 503, statusText: 'Unavailable' });

    expect(harness.session.retryNeeded()).toBe(true);
    expect(harness.session.state().kind).toBe('signed-in');
    expect(answer).toBeUndefined();
    expect(failed).toBe(false);
    harness.http.expectNone('/api/identity/users');

    harness.session.retry();

    expect(harness.session.retryNeeded()).toBe(false);
    harness.http.expectOne('/api/identity/users').flush([{ id: 1 }]);
    expect(answer).toEqual([{ id: 1 }]);
  });

  it.each([
    [400, 'validation-failed'],
    [403, 'permission-denied'],
    [404, 'not-found'],
    [503, 'upstream-unavailable'],
    [500, 'server-error'],
  ])('passes %s %s through', (status, type) => {
    let failure: HttpErrorResponse | undefined;
    client.get('/api/identity/users').subscribe({ error: (error: HttpErrorResponse) => (failure = error) });

    harness.http.expectOne('/api/identity/users').flush({ type }, { status, statusText: 'Error' });

    expect(failure?.status).toBe(status);
    expect(harness.session.state().kind).toBe('signed-in');
    expect(harness.session.retryNeeded()).toBe(false);
  });
});
