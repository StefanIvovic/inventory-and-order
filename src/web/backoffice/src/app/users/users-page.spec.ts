import { TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { LookupsApi } from '../lookups/lookups-api';
import { RolesApi } from '../roles/roles-api';
import { tenantRoles } from '../roles/testing/tenant-roles';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { StaffItem, StaffList, UsersApi } from './users-api';
import { UsersPage, searchDelay } from './users-page';

const platformAdmin = { ...staffUser, userType: 'platform' as const, tenantId: null };
const statuses = [
  { id: 's1', code: 'active', name: 'Working' },
  { id: 's2', code: 'disabled', name: 'Switched off' },
];

function user(email: string, change: Partial<StaffItem> = {}): StaffItem {
  return {
    id: email,
    email,
    userType: 'staff',
    disabled: false,
    roles: [],
    createdAt: '2026-11-20T09:00:00Z',
    actions: { canDisable: true, canAssignRoles: true },
    ...change,
  };
}

function list(items: StaffItem[], change: Partial<StaffList> = {}): StaffList {
  return { items, nextCursor: null, actions: { canCreate: true }, ...change };
}

const forbidden = { type: 'https://tools.ietf.org/html/rfc9110#section-15.5.4', title: 'Forbidden', status: 403, code: 'permission-denied' };

describe('UsersPage', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => {
    vi.useRealTimers();
    harness.http.verify();
  });

  function render() {
    const fixture = TestBed.createComponent(UsersPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const detect = () => fixture.detectChanges();
    const button = (text: string) => Array.from(element.querySelectorAll<HTMLButtonElement>('button')).find((candidate) => candidate.textContent?.trim() === text);
    const rows = () =>
      Array.from(element.querySelectorAll('tr[mat-row]')).map((row) => Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent?.trim()));
    const range = () => element.querySelector('.range')?.textContent?.trim();
    const choose = async (filter: string, option: string) => {
      element.querySelector<HTMLElement>(`mat-select[data-filter="${filter}"] .mat-mdc-select-trigger`)?.click();
      detect();
      await fixture.whenStable();
      Array.from(document.querySelectorAll<HTMLElement>('mat-option')).find((candidate) => candidate.textContent?.trim() === option)?.click();
      detect();
    };
    const options = async (filter: string) => {
      const select = element.querySelector<HTMLElement>(`mat-select[data-filter="${filter}"]`)!;
      select.querySelector<HTMLElement>('.mat-mdc-select-trigger')?.click();
      detect();
      await fixture.whenStable();
      const panel = document.getElementById(select.getAttribute('aria-controls') ?? '');
      const texts = Array.from(panel?.querySelectorAll('mat-option') ?? []).map((option) => option.textContent?.trim());
      document.querySelector<HTMLElement>('.cdk-overlay-backdrop')?.click();
      detect();
      await fixture.whenStable();
      return texts;
    };
    return { element, detect, button, rows, range, choose, options };
  }

  function listRequest(): TestRequest {
    return harness.http.expectOne((request) => request.url === UsersApi.url);
  }

  function params(request: TestRequest): Record<string, string | null> {
    return Object.fromEntries(request.request.params.keys().map((key) => [key, request.request.params.get(key)]));
  }

  function flushStart(first: StaffList, extras?: { roles?: unknown; statuses?: unknown; failRoles?: number; failStatuses?: number }) {
    const page = listRequest();
    const roles = harness.http.expectOne(RolesApi.url);
    const lookup = harness.http.expectOne((request) => request.url === LookupsApi.valuesUrl('staff-status'));
    page.flush(first);
    if (extras?.failRoles) {
      roles.flush(extras.failRoles === 403 ? forbidden : {}, { status: extras.failRoles, statusText: 'Error' });
    } else {
      roles.flush(extras?.roles ?? tenantRoles);
    }

    if (extras?.failRoles) {
      return page;
    }

    if (extras?.failStatuses) {
      lookup.flush({}, { status: extras.failStatuses, statusText: 'Error' });
    } else {
      lookup.flush({ items: extras?.statuses ?? statuses, nextCursor: null });
    }

    return page;
  }

  it('is not available to the platform admin and asks for nothing', async () => {
    await loadAs(harness, platformAdmin);
    const { element } = render();

    expect(element.textContent).toContain("This page isn't available to you.");
    harness.http.expectNone(() => true);
  });

  it('loads the first page with the total, the roles and the status list together, then shows names from them', async () => {
    await loadAs(harness, staffUser);
    const view = render();
    expect(view.element.querySelector('mat-progress-bar')).not.toBeNull();

    const page = flushStart(
      list(
        [
          user('ann@demo.test', { roles: ['identity.administrator', 'identity.viewer'] }),
          user('bob@demo.test', { disabled: true, actions: { canDisable: false, canAssignRoles: false } }),
        ],
        { total: 2 },
      ),
    );
    view.detect();

    expect(params(page)).toEqual({ size: '25', includeTotal: 'true' });
    expect(view.rows().map((row) => row.slice(0, 3))).toEqual([
      ['ann@demo.test', 'Administrator, Viewer', 'Working'],
      ['bob@demo.test', 'No roles', 'Switched off'],
    ]);
    expect(view.rows()[0]?.[3]).toMatch(/2026/);
    expect(view.range()).toBe('1–2 of 2 users');
  });

  it.each([
    ['the roles', { failRoles: 500 }, 'Users could not be loaded.'],
    ['the status list', { failStatuses: 500 }, 'Users could not be loaded.'],
    ['the roles with 403', { failRoles: 403 }, "You don't have access to users."],
  ])('shows the page error when %s cannot be loaded', async (_, extras, text) => {
    await loadAs(harness, staffUser);
    const view = render();

    flushStart(list([user('ann@demo.test')]), extras);
    view.detect();

    expect(view.element.textContent).toContain(text);
    expect(view.rows()).toEqual([]);
  });

  it('shows no-access on 403 and try again on other failures', async () => {
    await loadAs(harness, staffUser);
    const view = render();
    listRequest().flush(forbidden, { status: 403, statusText: 'Forbidden' });
    harness.http.expectOne(RolesApi.url);
    harness.http.expectOne((request) => request.url === LookupsApi.valuesUrl('staff-status'));
    view.detect();
    expect(view.element.textContent).toContain("You don't have access to users.");

    const second = render();
    listRequest().flush({}, { status: 500, statusText: 'Error' });
    harness.http.expectOne(RolesApi.url);
    harness.http.expectOne((request) => request.url === LookupsApi.valuesUrl('staff-status'));
    second.detect();
    expect(second.element.textContent).toContain('Users could not be loaded.');

    second.button('Try again')?.click();
    flushStart(list([user('ann@demo.test')]));
    second.detect();
    expect(second.rows().length).toBe(1);
  });

  it.each([
    [true, true],
    [false, false],
  ])('shows Add staff user only when allowed (canCreate %s), and row actions only from their flags', async (canCreate, shown) => {
    await loadAs(harness, staffUser);
    const view = render();
    flushStart(
      list([user('ann@demo.test', { actions: { canDisable: false, canAssignRoles: true } }), user('bob@demo.test', { actions: { canDisable: true, canAssignRoles: false } })], {
        actions: { canCreate },
      }),
    );
    view.detect();

    expect(view.button('Add staff user') !== undefined).toBe(shown);
    expect(view.rows().map((row) => row[4])).toEqual(['Roles', 'Disable']);
  });

  it('takes the filter options from the status list and the roles, writing none itself', async () => {
    await loadAs(harness, staffUser);
    const view = render();
    flushStart(list([user('ann@demo.test')]), {
      roles: { roles: [{ name: 'identity.auditor', displayName: 'Auditor', permissions: [] }], canAssign: false },
      statuses: [{ id: 'x', code: 'paused', name: 'Paused' }],
    });
    view.detect();

    expect(await view.options('status')).toEqual(['All', 'Paused']);
    expect(await view.options('role')).toEqual(['Any', 'Auditor']);
  });

  it('pages with Next from the prefetched page and Previous from the seen token', async () => {
    await loadAs(harness, staffUser);
    const view = render();
    flushStart(list([user('a@demo.test'), user('b@demo.test')], { total: 5, nextCursor: 'c2' }));
    const prefetch = listRequest();
    expect(params(prefetch)).toEqual({ size: '25', after: 'c2' });
    prefetch.flush(list([user('c@demo.test'), user('d@demo.test')], { nextCursor: 'c3' }));
    view.detect();
    expect(view.button('Previous')?.disabled).toBe(true);

    view.button('Next')?.click();
    view.detect();
    expect(view.rows().map((row) => row[0])).toEqual(['c@demo.test', 'd@demo.test']);
    expect(view.range()).toBe('26–27 of 5 users');
    const third = listRequest();
    expect(params(third)).toEqual({ size: '25', after: 'c3' });
    third.flush(list([user('e@demo.test')]));
    view.detect();

    view.button('Previous')?.click();
    view.detect();
    expect(view.rows().map((row) => row[0])).toEqual(['a@demo.test', 'b@demo.test']);
    harness.http.expectNone((request) => request.url === UsersApi.url);
    expect(view.button('Previous')?.disabled).toBe(true);
  });

  it('asks for a page that is not cached yet and disables Next on the last page', async () => {
    await loadAs(harness, staffUser);
    const view = render();
    flushStart(list([user('a@demo.test')], { total: 2, nextCursor: 'c2' }));
    listRequest().flush({}, { status: 500, statusText: 'Error' });
    view.detect();

    view.button('Next')?.click();
    const page = listRequest();
    expect(params(page)).toEqual({ size: '25', after: 'c2' });
    page.flush(list([user('b@demo.test')]));
    view.detect();

    expect(view.rows().map((row) => row[0])).toEqual(['b@demo.test']);
    expect(view.button('Next')?.disabled).toBe(true);
  });

  it('restarts at the first page with the total when the size, status or role changes', async () => {
    await loadAs(harness, staffUser);
    const view = render();
    flushStart(list([user('a@demo.test')], { total: 1 }));
    view.detect();

    await view.choose('size', '10');
    expect(params(listRequest())).toEqual({ size: '10', includeTotal: 'true' });

    await view.choose('status', 'Switched off');
    expect(params(listRequest())).toEqual({ size: '10', status: 'disabled', includeTotal: 'true' });

    await view.choose('role', 'Viewer');
    const filtered = listRequest();
    expect(params(filtered)).toEqual({ size: '10', status: 'disabled', role: 'identity.viewer', includeTotal: 'true' });
    filtered.flush(list([], { total: 0 }));
    view.detect();

    expect(view.element.textContent).toContain('No users match these filters.');
    view.button('Clear filters')?.click();
    expect(params(listRequest())).toEqual({ size: '10', includeTotal: 'true' });
  });

  it('searches 300 ms after typing stops', async () => {
    vi.useFakeTimers();
    await loadAs(harness, staffUser);
    const view = render();
    flushStart(list([user('a@demo.test')], { total: 1 }));
    view.detect();
    const input = view.element.querySelector<HTMLInputElement>('input[data-filter="q"]')!;

    input.value = 'an';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(searchDelay - 1);
    input.value = 'ann';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(searchDelay - 1);
    harness.http.expectNone((request) => request.url === UsersApi.url);

    vi.advanceTimersByTime(1);
    expect(params(listRequest())).toEqual({ size: '25', q: 'ann', includeTotal: 'true' });
  });

  it('restarts at the first page when the page token is refused', async () => {
    await loadAs(harness, staffUser);
    const view = render();
    flushStart(list([user('a@demo.test')], { total: 2, nextCursor: 'c2' }));
    listRequest().flush({}, { status: 500, statusText: 'Error' });
    view.detect();

    view.button('Next')?.click();
    listRequest().flush(
      { type: 'https://tools.ietf.org/html/rfc4918#section-11.2', title: 'Unprocessable', status: 422, code: 'invalid-cursor' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );

    const restart = listRequest();
    expect(params(restart)).toEqual({ size: '25', includeTotal: 'true' });
    restart.flush(list([user('a@demo.test')], { total: 1 }));
    view.detect();
    expect(view.range()).toBe('1–1 of 1 users');
  });

  it('reloads and confirms after a staff user is added', async () => {
    await loadAs(harness, staffUser);
    const open = vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({ afterClosed: () => of('clerk@demo.test') } as never);
    const snack = vi.spyOn(TestBed.inject(MatSnackBar), 'open');
    const view = render();
    flushStart(list([user('a@demo.test')]));
    view.detect();

    view.button('Add staff user')?.click();

    expect(open).toHaveBeenCalled();
    expect(snack).toHaveBeenCalledWith('Staff user clerk@demo.test added.', undefined, expect.objectContaining({ duration: 5000 }));
    expect(params(listRequest())).toEqual({ size: '25', includeTotal: 'true' });
  });

  it('opens the roles dialog with the roles from the API and reloads only after a change', async () => {
    await loadAs(harness, staffUser);
    const results = [false, true];
    const open = vi.spyOn(TestBed.inject(MatDialog), 'open').mockImplementation(() => ({ afterClosed: () => of(results.shift()) }) as never);
    const view = render();
    flushStart(list([user('a@demo.test')]));
    view.detect();

    view.button('Roles')?.click();
    expect(open.mock.calls[0]?.[1]).toEqual({ data: { user: user('a@demo.test'), roles: tenantRoles.roles } });
    harness.http.expectNone((request) => request.url === UsersApi.url);

    view.button('Roles')?.click();
    listRequest();
  });

  describe('disable', () => {
    async function confirmDisable(confirmed: boolean) {
      await loadAs(harness, staffUser);
      const open = vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({ afterClosed: () => of(confirmed) } as never);
      const snack = vi.spyOn(TestBed.inject(MatSnackBar), 'open');
      const view = render();
      flushStart(list([user('bob@demo.test')]));
      view.detect();
      view.button('Disable')?.click();
      return { open, snack };
    }

    it('asks for confirmation with the email and sends nothing when cancelled', async () => {
      const { open } = await confirmDisable(false);

      expect(open.mock.calls[0]?.[1]).toEqual({
        data: { title: 'Disable user', text: 'Disable bob@demo.test? They will lose access to the backoffice.', confirm: 'Disable' },
      });
      harness.http.expectNone(() => true);
    });

    it('disables, reloads and confirms', async () => {
      const { snack } = await confirmDisable(true);

      harness.http.expectOne('/api/identity/users/bob%40demo.test/disable').flush(null, { status: 204, statusText: 'No Content' });

      expect(snack).toHaveBeenCalledWith('bob@demo.test disabled.', undefined, expect.objectContaining({ duration: 5000 }));
      listRequest();
    });

    it.each([
      ['cannot-disable-self', "You can't disable your own account."],
      ['something-else', 'The user could not be disabled.'],
    ])('shows %s without reloading', async (code, message) => {
      const { snack } = await confirmDisable(true);

      harness.http
        .expectOne('/api/identity/users/bob%40demo.test/disable')
        .flush({ type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10', title: 'Conflict', status: 409, code }, { status: 409, statusText: 'Conflict' });

      expect(snack).toHaveBeenCalledWith(message, undefined, expect.objectContaining({ duration: 5000 }));
      harness.http.expectNone((request) => request.url === UsersApi.url);
    });
  });
});
