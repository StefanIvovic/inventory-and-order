import { TestBed } from '@angular/core/testing';
import { SessionHarness, loadAs, setUpSession } from '../session/testing/session-harness';
import { SignIn } from './sign-in';

describe('SignIn', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => harness.http.verify());

  it('navigates to the gateway login with the current page as return path', async () => {
    harness.location.currentPath = '/backoffice/orders';
    await loadAs(harness, null);
    const fixture = TestBed.createComponent(SignIn);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('button')?.click();

    expect(harness.location.assigned).toEqual(['/bff/backoffice/login?returnUrl=' + encodeURIComponent('/backoffice/orders')]);
  });

  it('shows the sign-in error message', async () => {
    harness.location.currentSearch = '?signin-error=access_denied';
    await loadAs(harness, null);
    const fixture = TestBed.createComponent(SignIn);
    fixture.detectChanges();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
    expect(alert?.textContent).toContain("You don't have access to the backoffice.");
  });

  it('shows no error line without an error', async () => {
    await loadAs(harness, null);
    const fixture = TestBed.createComponent(SignIn);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')).toBeNull();
  });
});
