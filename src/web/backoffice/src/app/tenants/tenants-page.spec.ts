import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { TenantList, TenantsApi } from './tenants-api';
import { TenantsPage } from './tenants-page';

const platformAdmin = { ...staffUser, userType: 'platform' as const, tenantId: null };

const twoTenants: TenantList = {
  items: [
    { id: '1', slug: 'older', name: 'Older Ltd', createdAt: '2026-11-01T09:00:00Z' },
    { id: '2', slug: 'newer', name: 'Newer Ltd', createdAt: '2026-11-20T09:00:00Z' },
  ],
  actions: { canCreate: true },
};

describe('TenantsPage', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => harness.http.verify());

  async function render(): Promise<{ element: HTMLElement; detect: () => void }> {
    const fixture = TestBed.createComponent(TenantsPage);
    fixture.detectChanges();
    return { element: fixture.nativeElement as HTMLElement, detect: () => fixture.detectChanges() };
  }

  it('is not available to staff and asks for nothing', async () => {
    await loadAs(harness, staffUser);
    const { element } = await render();

    expect(element.textContent).toContain("This page isn't available to you.");
    harness.http.expectNone(TenantsApi.url);
  });

  it('shows a progress bar while loading', async () => {
    await loadAs(harness, platformAdmin);
    const { element } = await render();

    expect(element.querySelector('mat-progress-bar')).not.toBeNull();
    harness.http.expectOne(TenantsApi.url).flush(twoTenants);
  });

  it('lists tenants newest first', async () => {
    await loadAs(harness, platformAdmin);
    const { element, detect } = await render();
    harness.http.expectOne(TenantsApi.url).flush(twoTenants);
    detect();

    const rows = Array.from(element.querySelectorAll('tr[mat-row]')).map((row) =>
      Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent?.trim()),
    );
    expect(rows.map((row) => row[0])).toEqual(['newer', 'older']);
    expect(rows[0]?.[1]).toBe('Newer Ltd');
    expect(rows[0]?.[2]).toMatch(/2026/);
    expect(element.querySelector('mat-progress-bar')).toBeNull();
  });

  it('says when there are no tenants', async () => {
    await loadAs(harness, platformAdmin);
    const { element, detect } = await render();
    harness.http.expectOne(TenantsApi.url).flush({ items: [], actions: { canCreate: true } });
    detect();

    expect(element.textContent).toContain('No tenants yet.');
  });

  it('offers to try again when loading fails', async () => {
    await loadAs(harness, platformAdmin);
    const { element, detect } = await render();
    harness.http.expectOne(TenantsApi.url).flush({}, { status: 500, statusText: 'Error' });
    detect();

    expect(element.textContent).toContain('Tenants could not be loaded.');
    Array.from(element.querySelectorAll('button')).find((button) => button.textContent?.includes('Try again'))?.click();
    harness.http.expectOne(TenantsApi.url).flush(twoTenants);
  });

  it.each([
    [true, true],
    [false, false],
  ])('shows Create tenant only when allowed (canCreate %s)', async (canCreate, shown) => {
    await loadAs(harness, platformAdmin);
    const { element, detect } = await render();
    harness.http.expectOne(TenantsApi.url).flush({ ...twoTenants, actions: { canCreate } });
    detect();

    const button = Array.from(element.querySelectorAll('button')).find((candidate) => candidate.textContent?.includes('Create tenant'));
    expect(button !== undefined).toBe(shown);
  });

  it('reloads and confirms after a tenant is created', async () => {
    await loadAs(harness, platformAdmin);
    const opened = { afterClosed: () => of('acme') };
    const dialog = TestBed.inject(MatDialog);
    const open = vi.spyOn(dialog, 'open').mockReturnValue(opened as never);
    const snack = vi.spyOn(TestBed.inject(MatSnackBar), 'open');
    const { element, detect } = await render();
    harness.http.expectOne(TenantsApi.url).flush(twoTenants);
    detect();

    Array.from(element.querySelectorAll('button')).find((button) => button.textContent?.includes('Create tenant'))?.click();

    expect(open).toHaveBeenCalled();
    harness.http.expectOne(TenantsApi.url).flush(twoTenants);
    expect(snack).toHaveBeenCalledWith('Tenant acme created.', undefined, expect.objectContaining({ duration: 5000 }));
  });

  it('does not reload when the dialog is cancelled', async () => {
    await loadAs(harness, platformAdmin);
    vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({ afterClosed: () => of(undefined) } as never);
    const { element, detect } = await render();
    harness.http.expectOne(TenantsApi.url).flush(twoTenants);
    detect();

    Array.from(element.querySelectorAll('button')).find((button) => button.textContent?.includes('Create tenant'))?.click();

    harness.http.expectNone(TenantsApi.url);
  });
});
