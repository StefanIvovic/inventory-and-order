import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { SessionHarness, customer, loadAs, setUpSession } from '../session/testing/session-harness';
import { Frame } from './frame';

describe('Frame', () => {
  let harness: SessionHarness;

  afterEach(() => harness.http.verify());

  function render(): { element: HTMLElement; detect: () => void } {
    const fixture = TestBed.createComponent(Frame);
    fixture.detectChanges();
    return { element: fixture.nativeElement as HTMLElement, detect: () => fixture.detectChanges() };
  }

  it('signs a visitor in with the current page as return path', async () => {
    harness = setUpSession('/shop/demo/some/page');
    await loadAs(harness, null);
    const { element } = render();

    element.querySelector<HTMLButtonElement>('mat-toolbar button')?.click();

    expect(harness.location.assigned).toEqual(['/bff/shop/demo/login?returnUrl=' + encodeURIComponent('/shop/demo/some/page')]);
  });

  it('shows the sign-in error message', async () => {
    harness = setUpSession('/shop/demo/', '?signin-error=access_denied');
    await loadAs(harness, null);
    const { element } = render();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain("This account can't be used in this shop.");
  });

  it('shows the account menu with the email for a customer', async () => {
    harness = setUpSession();
    await loadAs(harness, customer);
    const { element } = render();

    const trigger = element.querySelector<HTMLButtonElement>('[aria-label="Account menu"]');
    expect(trigger?.textContent).toContain('shopper@mail.test');
    expect(element.textContent).not.toContain('Sign in');

    trigger?.click();
    const items = Array.from(document.querySelectorAll<HTMLElement>('[mat-menu-item]')).map((item) => item.textContent?.trim());
    expect(items).toEqual(['Account', 'Sign out']);
  });

  it('shows the user id when there is no email', async () => {
    harness = setUpSession();
    await loadAs(harness, { ...customer, email: null });
    const { element } = render();

    expect(element.querySelector('[aria-label="Account menu"]')?.textContent).toContain('c-1');
  });

  it('signs out with the session sign-out address', async () => {
    harness = setUpSession();
    await loadAs(harness, customer);
    const { element } = render();

    element.querySelector<HTMLButtonElement>('[aria-label="Account menu"]')?.click();
    Array.from(document.querySelectorAll<HTMLButtonElement>('button[mat-menu-item]'))
      .find((item) => item.textContent?.includes('Sign out'))
      ?.click();

    expect(harness.location.assigned).toEqual(['/bff/shop/demo/logout?sid=abc']);
  });

  it('shows the retry banner for identity-unavailable and retries', async () => {
    harness = setUpSession();
    await loadAs(harness, customer);
    const { element, detect } = render();
    let answer: unknown;
    TestBed.inject(HttpClient).get('/api/identity/x').subscribe((value) => (answer = value));
    harness.http.expectOne('/api/identity/x').flush({ type: 'identity-unavailable' }, { status: 503, statusText: 'Unavailable' });
    detect();

    const banner = element.querySelector('[role="status"]');
    expect(banner?.textContent).toContain('Sign-in service is unavailable.');
    banner?.querySelector('button')?.click();
    harness.http.expectOne('/api/identity/x').flush(['ok']);
    detect();

    expect(answer).toEqual(['ok']);
    expect(element.querySelector('[role="status"]')).toBeNull();
  });

  it('turns into the visitor view when an API call answers 401', async () => {
    harness = setUpSession();
    await loadAs(harness, customer);
    const { element, detect } = render();
    TestBed.inject(HttpClient).get('/api/identity/x').subscribe({ error: () => undefined });
    harness.http.expectOne('/api/identity/x').flush({ type: 'session-ended' }, { status: 401, statusText: 'Unauthorized' });
    detect();

    expect(element.querySelector('mat-toolbar')?.textContent).toContain('Sign in');
    expect(element.querySelector('[aria-label="Account menu"]')).toBeNull();
  });
});
