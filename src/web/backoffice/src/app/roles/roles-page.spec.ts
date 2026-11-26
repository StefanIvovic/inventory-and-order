import { TestBed } from '@angular/core/testing';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { RolesApi } from './roles-api';
import { RolesPage } from './roles-page';
import { tenantRoles } from './testing/tenant-roles';

const platformAdmin = { ...staffUser, userType: 'platform' as const, tenantId: null };

describe('RolesPage', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => harness.http.verify());

  function render() {
    const fixture = TestBed.createComponent(RolesPage);
    fixture.detectChanges();
    return { element: fixture.nativeElement as HTMLElement, detect: () => fixture.detectChanges() };
  }

  it('is not available to the platform admin', async () => {
    await loadAs(harness, platformAdmin);
    const { element } = render();

    expect(element.textContent).toContain("This page isn't available to you.");
    harness.http.expectNone(RolesApi.url);
  });

  it('shows a progress bar, then each role with its permissions by display name', async () => {
    await loadAs(harness, staffUser);
    const { element, detect } = render();
    expect(element.querySelector('mat-progress-bar')).not.toBeNull();

    harness.http.expectOne(RolesApi.url).flush(tenantRoles);
    detect();

    const sections = Array.from(element.querySelectorAll('section')).map((section) => ({
      role: section.querySelector('h2')?.textContent?.trim(),
      permissions: Array.from(section.querySelectorAll('li')).map((item) => item.textContent?.trim()),
    }));
    expect(sections).toEqual([
      { role: 'Administrator', permissions: ['Assign roles', 'Manage users', 'View roles', 'View users'] },
      { role: 'Viewer', permissions: ['View roles', 'View users'] },
    ]);
    expect(element.textContent).not.toContain('identity.');
  });

  it('says when access is missing', async () => {
    await loadAs(harness, staffUser);
    const { element, detect } = render();

    harness.http
      .expectOne(RolesApi.url)
      .flush({ type: 'https://tools.ietf.org/html/rfc9110#section-15.5.4', title: 'Forbidden', status: 403, code: 'permission-denied' }, { status: 403, statusText: 'Forbidden' });
    detect();

    expect(element.textContent).toContain("You don't have access to roles.");
  });

  it('offers to try again when loading fails', async () => {
    await loadAs(harness, staffUser);
    const { element, detect } = render();
    harness.http.expectOne(RolesApi.url).flush({}, { status: 500, statusText: 'Error' });
    detect();

    expect(element.textContent).toContain('Roles could not be loaded.');
    Array.from(element.querySelectorAll('button')).find((button) => button.textContent?.includes('Try again'))?.click();
    harness.http.expectOne(RolesApi.url).flush(tenantRoles);
  });
});
