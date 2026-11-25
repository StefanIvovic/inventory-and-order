import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { SessionHarness, customer, loadAs, setUpSession } from './testing/session-harness';

describe('apiInterceptor', () => {
  let harness: SessionHarness;
  let client: HttpClient;

  beforeEach(async () => {
    harness = setUpSession();
    client = TestBed.inject(HttpClient);
    await loadAs(harness, customer);
  });

  afterEach(() => harness.http.verify());

  it('adds the storefront header to API calls only', () => {
    client.get('/api/identity/x').subscribe();
    client.get('/shop/x.json').subscribe();
    client.get('/bff/shop/demo/user').subscribe();

    expect(harness.http.expectOne('/api/identity/x').request.headers.get('X-Bff-App')).toBe('storefront');
    expect(harness.http.expectOne('/shop/x.json').request.headers.has('X-Bff-App')).toBe(false);
    expect(harness.http.expectOne('/bff/shop/demo/user').request.headers.has('X-Bff-App')).toBe(false);
  });

  it('adds nothing but the app header', () => {
    client.get('/api/identity/x').subscribe();

    expect(harness.http.expectOne('/api/identity/x').request.headers.keys()).toEqual(['X-Bff-App']);
  });

  it('a 401 turns the customer into a visitor and reaches the caller', () => {
    let failure: unknown;
    client.get('/api/identity/x').subscribe({ error: (error: unknown) => (failure = error) });

    harness.http.expectOne('/api/identity/x').flush({ type: 'session-ended' }, { status: 401, statusText: 'Unauthorized' });

    expect(harness.session().state()).toEqual({ kind: 'visitor', error: null });
    expect((failure as HttpErrorResponse).status).toBe(401);
  });

  it('a 503 identity-unavailable waits for retry and then sends the request again', () => {
    let answer: unknown;
    client.get('/api/identity/x').subscribe((value) => (answer = value));

    harness.http.expectOne('/api/identity/x').flush({ type: 'identity-unavailable' }, { status: 503, statusText: 'Unavailable' });

    expect(harness.session().retryNeeded()).toBe(true);
    expect(answer).toBeUndefined();
    harness.http.expectNone('/api/identity/x');

    harness.session().retry();

    harness.http.expectOne('/api/identity/x').flush(['ok']);
    expect(answer).toEqual(['ok']);
    expect(harness.session().retryNeeded()).toBe(false);
  });

  it.each([400, 403, 404, 500])('passes %s through', (status) => {
    let failure: HttpErrorResponse | undefined;
    client.get('/api/identity/x').subscribe({ error: (error: HttpErrorResponse) => (failure = error) });

    harness.http.expectOne('/api/identity/x').flush({ type: 'other' }, { status, statusText: 'Error' });

    expect(failure?.status).toBe(status);
    expect(harness.session().state().kind).toBe('signed-in');
  });
});
