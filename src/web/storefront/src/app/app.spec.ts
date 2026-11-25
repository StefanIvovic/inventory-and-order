import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { SessionHarness, customer, loadAs, setUpSession } from './session/testing/session-harness';

describe('App', () => {
  let harness: SessionHarness;

  afterEach(() => harness.http.verify());

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows only the progress bar while loading', async () => {
    harness = setUpSession();
    const loaded = harness.session().load();
    const element = render();

    expect(element.querySelector('mat-progress-bar')).not.toBeNull();
    expect(element.querySelector('mat-toolbar')).toBeNull();
    harness.http.expectOne('/bff/shop/demo/user').flush(customer);
    await loaded;
  });

  it('shows the shop frame to a visitor', async () => {
    harness = setUpSession();
    await loadAs(harness, null);
    const element = render();

    expect(element.querySelector('mat-toolbar')?.textContent).toContain('demo');
    expect(element.querySelector('mat-toolbar')?.textContent).toContain('Sign in');
  });

  it('shows the frame with the trouble banner when unavailable, and retries', async () => {
    harness = setUpSession();
    await loadAs(harness, null, 502);
    const element = render();

    expect(element.querySelector('mat-toolbar')?.textContent).toContain('demo');
    const banner = element.querySelector('[role="status"]');
    expect(banner?.textContent).toContain('The shop is having trouble right now.');
    banner?.querySelector('button')?.click();
    harness.http.expectOne('/bff/shop/demo/user').flush(customer);
  });

  it('shows shop not found for an invalid shop address', async () => {
    harness = setUpSession('/shop/Demo/');
    await harness.session().load();
    const element = render();

    expect(element.textContent).toContain("This shop doesn't exist.");
    expect(element.textContent).not.toContain('Sign in');
  });
});
