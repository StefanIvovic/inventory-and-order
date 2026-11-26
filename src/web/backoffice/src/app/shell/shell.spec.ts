import { BreakpointObserver, BreakpointState } from '@angular/cdk/layout';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { LookupsApi } from '../lookups/lookups-api';
import { Shell } from './shell';

const userTypes = [
  { id: 'c', code: 'customer', name: 'Customer' },
  { id: 'p', code: 'platform', name: 'Platform administrator' },
  { id: 's', code: 'staff', name: 'Staff' },
];

function flushUserTypes(harness: SessionHarness, status = 200): void {
  const request = harness.http.expectOne((candidate) => candidate.url === LookupsApi.valuesUrl('user-type'));
  if (status === 200) {
    request.flush({ items: userTypes, nextCursor: null });
  } else {
    request.flush({}, { status, statusText: 'Error' });
  }
}

@Component({ template: '' })
class Blank {}

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
    flushUserTypes(harness);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the email and Staff', async () => {
    const element = await render();

    expect(element.textContent).toContain('admin@demo.local');
    expect(element.textContent).toContain('Staff');
  });

  it('takes the user type name from the user-type list and shows none when the list fails', async () => {
    await loadAs(harness, staffUser);
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    flushUserTypes(harness, 500);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.role')).toBeNull();
    expect(element.textContent).not.toContain('staff');
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
    flushUserTypes(harness);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[role="status"]')?.textContent).toContain('Sign-in service is unavailable.');
    element.querySelector<HTMLButtonElement>('[role="status"] button')?.click();
    harness.http.expectOne('/api/identity/users').flush(['ok']);
    fixture.detectChanges();

    expect(answer).toEqual(['ok']);
    expect(element.querySelector('[role="status"]')).toBeNull();
  });

  function navigationEntries(element: HTMLElement): string[] {
    return Array.from(element.querySelectorAll('mat-nav-list a')).map((link) => link.textContent?.trim() ?? '');
  }

  it('shows Home and Tenants to the platform admin', async () => {
    const element = await render({ ...staffUser, userType: 'platform', tenantId: null });

    expect(navigationEntries(element)).toEqual(['Home', 'Tenants']);
  });

  it('shows Home, Users and Roles to staff', async () => {
    const element = await render();

    expect(navigationEntries(element)).toEqual(['Home', 'Users', 'Roles']);
  });

  it('marks the active entry', async () => {
    await loadAs(harness, { ...staffUser, userType: 'platform', tenantId: null });
    const router = TestBed.inject(Router);
    router.resetConfig([
      { path: '', component: Blank },
      { path: 'tenants', component: Blank },
    ]);
    const fixture = TestBed.createComponent(Shell);
    await router.navigateByUrl('/tenants');
    fixture.detectChanges();
    flushUserTypes(harness);
    await fixture.whenStable();
    fixture.detectChanges();

    const active = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('mat-nav-list a.active-entry'));
    expect(active.map((link) => link.textContent?.trim())).toEqual(['Tenants']);
  });

  it('collapses behind a menu button on narrow windows', async () => {
    TestBed.resetTestingModule();
    TestBed.overrideProvider(BreakpointObserver, {
      useValue: { observe: () => of<BreakpointState>({ matches: true, breakpoints: {} }) },
    });
    harness = setUpSession();
    await loadAs(harness, staffUser);
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    flushUserTypes(harness);
    const element = fixture.nativeElement as HTMLElement;

    const sidenav = element.querySelector('mat-sidenav');
    expect(sidenav?.classList.contains('mat-drawer-over')).toBe(true);
    const menu = element.querySelector<HTMLButtonElement>('[aria-label="Menu"]');
    expect(menu).not.toBeNull();
    expect(sidenav?.classList.contains('mat-drawer-opened')).toBe(false);

    menu?.click();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(sidenav?.classList.contains('mat-drawer-opened')).toBe(true);
  });

  it('has no menu button on wide windows', async () => {
    const element = await render();

    expect(element.querySelector('[aria-label="Menu"]')).toBeNull();
    expect(element.querySelector('mat-sidenav')?.classList.contains('mat-drawer-opened')).toBe(true);
  });
});
