import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { SessionHarness, loadAs, setUpSession, staffUser } from './session/testing/session-harness';

describe('App', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => harness.http.verify());

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows only the progress bar while loading', async () => {
    const loaded = harness.session.load();
    const element = render();

    expect(element.querySelector('mat-progress-bar')).not.toBeNull();
    expect(element.textContent).not.toContain('Sign in');
    expect(element.querySelector('mat-toolbar')).toBeNull();
    harness.http.expectOne('/bff/backoffice/user').flush(staffUser);
    await loaded;
  });

  it('shows the sign-in screen without a session', async () => {
    const fixture = TestBed.createComponent(App);
    await loadAs(harness, null);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('button')?.textContent).toContain('Sign in');
    expect(element.querySelector('mat-toolbar')).toBeNull();
  });

  it('shows the frame with a session', async () => {
    const fixture = TestBed.createComponent(App);
    await loadAs(harness, staffUser);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('mat-toolbar')?.textContent).toContain('admin@demo.local');
    expect(element.querySelector('mat-toolbar')?.textContent).toContain('Staff');
    expect(element.textContent).not.toContain('Sign in to continue');
  });

  it('shows the retry screen when the backoffice is unavailable', async () => {
    const fixture = TestBed.createComponent(App);
    await loadAs(harness, null, 502);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('The backoffice is unavailable');
    const retry = Array.from(element.querySelectorAll('button')).find((button) => button.textContent?.includes('Try again'));
    retry?.click();
    harness.http.expectOne('/bff/backoffice/user').flush(staffUser);
  });
});
