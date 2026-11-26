import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { background } from './loading.interceptor';
import { LoadingService, loadingDelay } from './loading.service';

const url = '/api/identity/users';

describe('loadingInterceptor', () => {
  let harness: SessionHarness;
  let client: HttpClient;
  let loading: LoadingService;

  beforeEach(async () => {
    vi.useFakeTimers();
    harness = setUpSession();
    client = TestBed.inject(HttpClient);
    loading = TestBed.inject(LoadingService);
    await loadAs(harness, staffUser);
    vi.advanceTimersByTime(0);
  });

  afterEach(() => {
    vi.useRealTimers();
    harness.http.verify();
  });

  function settle(): void {
    vi.advanceTimersByTime(0);
  }

  it('shows the bar for a slow request and hides it on the answer', () => {
    client.get(url).subscribe();
    vi.advanceTimersByTime(loadingDelay);
    expect(loading.visible()).toBe(true);

    harness.http.expectOne(url).flush([]);
    settle();

    expect(loading.visible()).toBe(false);
  });

  it('hides the bar on an error answer', () => {
    client.get(url).subscribe({ error: () => undefined });
    vi.advanceTimersByTime(loadingDelay);

    harness.http.expectOne(url).flush({}, { status: 500, statusText: 'Error' });
    settle();

    expect(loading.visible()).toBe(false);
  });

  it('hides the bar when the request is cancelled', () => {
    const subscription = client.get(url).subscribe();
    vi.advanceTimersByTime(loadingDelay);
    const request = harness.http.expectOne(url);

    subscription.unsubscribe();
    settle();

    expect(request.cancelled).toBe(true);
    expect(loading.visible()).toBe(false);
  });

  it('never counts a background request', () => {
    client.get(url, { context: background() }).subscribe();
    vi.advanceTimersByTime(loadingDelay * 5);

    expect(loading.visible()).toBe(false);
    harness.http.expectOne(url).flush([]);
  });

  it('hides the bar when the user request ends while a background request still runs', () => {
    client.get(`${url}?after=x`, { context: background() }).subscribe();
    client.get(url).subscribe();
    vi.advanceTimersByTime(loadingDelay);
    expect(loading.visible()).toBe(true);

    harness.http.expectOne(url).flush([]);
    settle();

    expect(loading.visible()).toBe(false);
    harness.http.expectOne(`${url}?after=x`).flush([]);
  });

  it('keeps the bar while a request waits for the sign-in retry', () => {
    client.get(url).subscribe();
    harness.http.expectOne(url).flush({ type: 'identity-unavailable' }, { status: 503, statusText: 'Unavailable' });
    vi.advanceTimersByTime(loadingDelay * 5);
    expect(loading.visible()).toBe(true);

    harness.session.retry();
    harness.http.expectOne(url).flush([]);
    settle();

    expect(loading.visible()).toBe(false);
  });
});
