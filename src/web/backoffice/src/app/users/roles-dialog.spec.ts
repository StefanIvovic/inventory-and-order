import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { LoadingService, loadingDelay } from '../loading/loading.service';
import { tenantRoles } from '../roles/testing/tenant-roles';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { RolesDialog, RolesDialogData } from './roles-dialog';
import { StaffItem, UsersApi } from './users-api';

const clerk: StaffItem = {
  id: 'u-9',
  email: 'clerk@demo.test',
  userType: 'staff',
  disabled: false,
  roles: ['identity.viewer'],
  createdAt: '2026-11-20T09:00:00Z',
  actions: { canDisable: true, canAssignRoles: true },
};

describe('RolesDialog', () => {
  let harness: SessionHarness;
  let closed: unknown[];

  beforeEach(async () => {
    closed = [];
    const data: RolesDialogData = { user: clerk, roles: tenantRoles.roles };
    TestBed.overrideProvider(MatDialogRef, { useValue: { close: (value: unknown) => closed.push(value) } });
    TestBed.overrideProvider(MAT_DIALOG_DATA, { useValue: data });
    harness = setUpSession();
    await loadAs(harness, staffUser);
  });

  afterEach(() => harness.http.verify());

  function render() {
    const fixture = TestBed.createComponent(RolesDialog);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const detect = () => fixture.detectChanges();
    const toggle = (role: string) => element.querySelector<HTMLElement>(`mat-slide-toggle[data-role="${role}"]`)!;
    const switchOf = (role: string) => toggle(role).querySelector<HTMLButtonElement>('button[role="switch"]')!;
    const isOn = (role: string) => switchOf(role).getAttribute('aria-checked') === 'true';
    const flip = (role: string) => {
      switchOf(role).click();
      detect();
    };
    const error = () => element.querySelector('[role="alert"]')?.textContent?.trim();
    const close = () => Array.from(element.querySelectorAll<HTMLButtonElement>('button')).find((button) => button.textContent?.trim() === 'Close')!.click();
    return { element, detect, toggle, switchOf, isOn, flip, error, close };
  }

  it('shows one switch per role from the API, in order, with its display name and the current roles', () => {
    const view = render();

    expect(view.element.querySelector('h2')?.textContent).toContain('Roles for clerk@demo.test');
    expect(Array.from(view.element.querySelectorAll('mat-slide-toggle')).map((toggle) => toggle.textContent?.trim())).toEqual(['Administrator', 'Viewer']);
    expect(view.isOn('identity.administrator')).toBe(false);
    expect(view.isOn('identity.viewer')).toBe(true);
  });

  it('assigns on, removes off, and disables the switch while saving', () => {
    const view = render();

    view.flip('identity.administrator');
    expect(view.switchOf('identity.administrator').disabled).toBe(true);
    const assign = harness.http.expectOne(UsersApi.roleUrl('u-9', 'identity.administrator'));
    expect(assign.request.method).toBe('PUT');
    assign.flush(null, { status: 204, statusText: 'No Content' });
    view.detect();
    expect(view.switchOf('identity.administrator').disabled).toBe(false);
    expect(view.isOn('identity.administrator')).toBe(true);

    view.flip('identity.viewer');
    const remove = harness.http.expectOne(UsersApi.roleUrl('u-9', 'identity.viewer'));
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null, { status: 204, statusText: 'No Content' });
    view.detect();
    expect(view.isOn('identity.viewer')).toBe(false);

    view.close();
    expect(closed).toEqual([true]);
  });

  it.each([
    ['cannot-remove-own-admin', "You can't remove your own Administrator role."],
    ['user-disabled', 'This user is disabled.'],
    ['other', 'The role could not be changed.'],
  ])('shows %s and puts the switch back', (code, message) => {
    const view = render();

    view.flip('identity.viewer');
    harness.http
      .expectOne(UsersApi.roleUrl('u-9', 'identity.viewer'))
      .flush({ type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10', title: 'Conflict', status: 409, code }, { status: 409, statusText: 'Conflict' });
    view.detect();

    expect(view.error()).toBe(message);
    expect(view.isOn('identity.viewer')).toBe(true);
    view.close();
    expect(closed).toEqual([false]);
  });

  it.each([
    ['answer', 204],
    ['error answer', 409],
  ])('shows the loading bar while a role switch saves, until its %s', (_, status) => {
    vi.useFakeTimers();
    try {
      const loading = TestBed.inject(LoadingService);
      const view = render();

      view.flip('identity.administrator');
      vi.advanceTimersByTime(loadingDelay);
      expect(loading.visible()).toBe(true);

      harness.http.expectOne(UsersApi.roleUrl('u-9', 'identity.administrator')).flush(null, { status, statusText: 'Answer' });
      vi.advanceTimersByTime(0);
      view.detect();

      expect(loading.visible()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('keeps the switch waiting while the sign-in service is unavailable, then re-sends on retry', () => {
    const view = render();

    view.flip('identity.administrator');
    harness.http.expectOne(UsersApi.roleUrl('u-9', 'identity.administrator')).flush({ type: 'identity-unavailable' }, { status: 503, statusText: 'Unavailable' });
    view.detect();

    expect(view.element.querySelector('[role="status"]')?.textContent).toContain('Sign-in service is unavailable.');
    expect(view.switchOf('identity.administrator').disabled).toBe(true);
    view.element.querySelector<HTMLButtonElement>('[role="status"] button')?.click();
    harness.http.expectOne(UsersApi.roleUrl('u-9', 'identity.administrator')).flush(null, { status: 204, statusText: 'No Content' });
    view.detect();

    expect(view.isOn('identity.administrator')).toBe(true);
    expect(view.switchOf('identity.administrator').disabled).toBe(false);
  });
});
