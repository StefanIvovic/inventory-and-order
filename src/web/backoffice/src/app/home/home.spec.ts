import { TestBed } from '@angular/core/testing';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { Home } from './home';

describe('Home', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => harness.http.verify());

  it('links the platform admin to the tenants', async () => {
    await loadAs(harness, { ...staffUser, userType: 'platform', tenantId: null });
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector('a');
    expect(link?.textContent).toContain('Manage tenants');
    expect(link?.getAttribute('href')).toBe('/tenants');
  });

  it('shows no tenants link to staff', async () => {
    await loadAs(harness, staffUser);
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('a')).toBeNull();
  });
});
