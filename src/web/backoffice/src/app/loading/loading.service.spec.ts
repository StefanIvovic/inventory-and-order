import { TestBed } from '@angular/core/testing';
import { LoadingService, loadingDelay } from './loading.service';

describe('LoadingService', () => {
  let loading: LoadingService;

  beforeEach(() => {
    vi.useFakeTimers();
    loading = TestBed.inject(LoadingService);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the bar only once a wait has lasted the delay', () => {
    loading.begin();

    vi.advanceTimersByTime(loadingDelay - 1);
    expect(loading.visible()).toBe(false);

    vi.advanceTimersByTime(1);
    expect(loading.visible()).toBe(true);
  });

  it('uses 200 ms', () => {
    expect(loadingDelay).toBe(200);
  });

  it('never shows the bar for a wait that ends before the delay', () => {
    const end = loading.begin();
    vi.advanceTimersByTime(loadingDelay - 1);
    end();

    vi.advanceTimersByTime(loadingDelay * 5);

    expect(loading.visible()).toBe(false);
  });

  it('keeps the bar until the last of overlapping waits ends', () => {
    const first = loading.begin();
    const second = loading.begin();
    vi.advanceTimersByTime(loadingDelay);

    first();
    vi.advanceTimersByTime(loadingDelay);
    expect(loading.visible()).toBe(true);

    second();
    expect(loading.visible()).toBe(true);
    vi.advanceTimersByTime(0);
    expect(loading.visible()).toBe(false);
  });

  it('keeps the bar when one wait ends and another begins in the same task', () => {
    const first = loading.begin();
    vi.advanceTimersByTime(loadingDelay);

    first();
    const second = loading.begin();
    vi.advanceTimersByTime(loadingDelay);
    expect(loading.visible()).toBe(true);

    second();
    vi.advanceTimersByTime(0);
    expect(loading.visible()).toBe(false);
  });

  it('keeps counting the delay from the first wait when waits follow each other before it', () => {
    const first = loading.begin();
    vi.advanceTimersByTime(loadingDelay - 50);
    first();
    loading.begin();

    vi.advanceTimersByTime(50);

    expect(loading.visible()).toBe(true);
  });

  it('ignores a second call of the same end', () => {
    const first = loading.begin();
    loading.begin();
    vi.advanceTimersByTime(loadingDelay);

    first();
    first();
    vi.advanceTimersByTime(0);

    expect(loading.visible()).toBe(true);
  });

  it('tracks a step that succeeds and settles with its value', async () => {
    let finish: (value: string) => void = () => undefined;
    const tracked = loading.track(new Promise<string>((resolve) => (finish = resolve)));
    vi.advanceTimersByTime(loadingDelay);
    expect(loading.visible()).toBe(true);

    finish('done');

    await expect(tracked).resolves.toBe('done');
    vi.advanceTimersByTime(0);
    expect(loading.visible()).toBe(false);
  });

  it('tracks a step that fails and settles with its error', async () => {
    let fail: (error: Error) => void = () => undefined;
    const tracked = loading.track(new Promise<string>((_, reject) => (fail = reject)));
    vi.advanceTimersByTime(loadingDelay);
    expect(loading.visible()).toBe(true);

    fail(new Error('failed'));

    await expect(tracked).rejects.toThrow('failed');
    vi.advanceTimersByTime(0);
    expect(loading.visible()).toBe(false);
  });
});
