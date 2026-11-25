import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { Shell } from './shell';

describe('Shell', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => harness.http.verify());

  async function render(user = staffUser): Promise<HTMLElement> {
    await loadAs(harness, user);
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the email and Staff', async () => {
    const element = await render();

    expect(element.textContent).toContain('admin@demo.local');
    expect(element.textContent).toContain('Staff');
  });

  it('shows Platform administrator for the platform admin', async () => {
    const element = await render({ ...staffUser, userType: 'platform', tenantId: null });

    expect(element.textContent).toContain('Platform administrator');
  });

  it('shows the user id when there is no email', async () => {
    const element = await render({ ...staffUser, email: null });

    expect(element.textContent).toContain('u-1');
  });

  it('signs out with the session sign-out address', async () => {
    const element = await render();

    const trigger = element.querySelector<HTMLButtonElement>('[aria-label="User menu"]');
    trigger?.click();
    const signOut = Array.from(document.querySelectorAll<HTMLButtonElement>('button[mat-menu-item]')).find((button) =>
      button.textContent?.includes('Sign out'),
    );
    signOut?.click();

    expect(harness.location.assigned).toEqual(['/bff/backoffice/logout?sid=abc']);
  });

  it('shows the retry banner when Identity is unavailable and retries', async () => {
    const fixture = TestBed.createComponent(Shell);
    await loadAs(harness, staffUser);
    let answer: unknown;
    TestBed.inject(HttpClient).get('/api/identity/users').subscribe((value) => (answer = value));
    harness.http.expectOne('/api/identity/users').flush({ type: 'identity-unavailable' }, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[role="status"]')?.textContent).toContain('Sign-in service is unavailable.');
    element.querySelector<HTMLButtonElement>('[role="status"] button')?.click();
    harness.http.expectOne('/api/identity/users').flush(['ok']);
    fixture.detectChanges();

    expect(answer).toEqual(['ok']);
    expect(element.querySelector('[role="status"]')).toBeNull();
  });
});
