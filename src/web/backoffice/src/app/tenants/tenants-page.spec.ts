import { TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { BACKGROUND } from '../loading/loading.interceptor';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { TenantItem, TenantList, TenantsApi } from './tenants-api';
import { TenantsPage } from './tenants-page';

const platformAdmin = { ...staffUser, userType: 'platform' as const, tenantId: null };

function tenant(slug: string): TenantItem {
  return { id: slug, slug, name: `${slug} Ltd`, createdAt: '2026-11-20T09:00:00Z' };
}

function list(slugs: string[], change: Partial<TenantList> = {}): TenantList {
  return { items: slugs.map(tenant), nextCursor: null, actions: { canCreate: true }, ...change };
}

describe('TenantsPage', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => harness.http.verify());

  function render() {
    const fixture = TestBed.createComponent(TenantsPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const detect = () => fixture.detectChanges();
    const button = (text: string) => Array.from(element.querySelectorAll<HTMLButtonElement>('button')).find((candidate) => candidate.textContent?.trim() === text);
    const rows = () =>
      Array.from(element.querySelectorAll('tr[mat-row]')).map((row) => Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent?.trim()));
    const range = () => element.querySelector('.range')?.textContent?.trim();
    const resize = async (size: string) => {
      element.querySelector<HTMLElement>('mat-select[data-filter="size"] .mat-mdc-select-trigger')?.click();
      detect();
      await fixture.whenStable();
      Array.from(document.querySelectorAll<HTMLElement>('mat-option')).find((option) => option.textContent?.trim() === size)?.click();
      detect();
    };
    const sizes = async () => {
      element.querySelector<HTMLElement>('mat-select[data-filter="size"] .mat-mdc-select-trigger')?.click();
      detect();
      await fixture.whenStable();
      const texts = Array.from(document.querySelectorAll<HTMLElement>('mat-option')).map((option) => option.textContent?.trim());
      document.querySelector<HTMLElement>('.cdk-overlay-backdrop')?.click();
      detect();
      await fixture.whenStable();
      return texts;
    };
    return { element, detect, button, rows, range, resize, sizes };
  }

  function listRequest(): TestRequest {
    return harness.http.expectOne((request) => request.url === TenantsApi.url);
  }

  function params(request: TestRequest): Record<string, string | null> {
    return Object.fromEntries(request.request.params.keys().map((key) => [key, request.request.params.get(key)]));
  }

  it('is not available to staff and asks for nothing', async () => {
    await loadAs(harness, staffUser);
    const { element } = render();

    expect(element.textContent).toContain("This page isn't available to you.");
    harness.http.expectNone((request) => request.url === TenantsApi.url);
  });

  it('shows a progress bar while the first page loads', async () => {
    await loadAs(harness, platformAdmin);
    const { element } = render();

    expect(element.querySelector('mat-progress-bar')).not.toBeNull();
    listRequest().flush(list(['acme']));
  });

  it('shows the first page with the total in the order the service gives', async () => {
    await loadAs(harness, platformAdmin);
    const view = render();
    const first = listRequest();
    expect(params(first)).toEqual({ size: '25', includeTotal: 'true' });
    first.flush(list(['zeta', 'acme'], { total: 2 }));
    view.detect();

    expect(view.rows().map((row) => row[0])).toEqual(['zeta', 'acme']);
    expect(view.rows()[0]?.[1]).toBe('zeta Ltd');
    expect(view.rows()[0]?.[2]).toMatch(/2026/);
    expect(view.range()).toBe('1–2 of 2 tenants');
    expect(view.element.querySelector('mat-progress-bar')).toBeNull();
  });

  it('offers 10, 25 and 50 rows per page and restarts with the total when the size changes', async () => {
    await loadAs(harness, platformAdmin);
    const view = render();
    listRequest().flush(list(['acme'], { total: 1 }));
    view.detect();

    expect(await view.sizes()).toEqual(['10', '25', '50']);
    await view.resize('10');

    expect(params(listRequest())).toEqual({ size: '10', includeTotal: 'true' });
  });

  it('pages with Next from the prefetched page in the background and Previous from the seen page', async () => {
    await loadAs(harness, platformAdmin);
    const view = render();
    listRequest().flush(list(['a-1', 'a-2'], { total: 5, nextCursor: 'c2' }));
    const prefetch = listRequest();
    expect(params(prefetch)).toEqual({ size: '25', after: 'c2' });
    expect(prefetch.request.context.get(BACKGROUND)).toBe(true);
    prefetch.flush(list(['a-3', 'a-4'], { nextCursor: 'c3' }));
    view.detect();
    expect(view.button('Previous')?.disabled).toBe(true);

    view.button('Next')?.click();
    view.detect();
    expect(view.rows().map((row) => row[0])).toEqual(['a-3', 'a-4']);
    expect(view.range()).toBe('26–27 of 5 tenants');
    const third = listRequest();
    expect(third.request.context.get(BACKGROUND)).toBe(true);
    third.flush(list(['a-5']));
    view.detect();

    view.button('Previous')?.click();
    view.detect();
    expect(view.rows().map((row) => row[0])).toEqual(['a-1', 'a-2']);
    harness.http.expectNone((request) => request.url === TenantsApi.url);
    expect(view.button('Previous')?.disabled).toBe(true);
  });

  it('asks for a page that is not cached yet, in the foreground, and disables Next on the last page', async () => {
    await loadAs(harness, platformAdmin);
    const view = render();
    listRequest().flush(list(['a-1'], { total: 2, nextCursor: 'c2' }));
    listRequest().flush({}, { status: 500, statusText: 'Error' });
    view.detect();

    view.button('Next')?.click();
    const page = listRequest();
    expect(params(page)).toEqual({ size: '25', after: 'c2' });
    expect(page.request.context.get(BACKGROUND)).toBe(false);
    page.flush(list(['a-2']));
    view.detect();

    expect(view.rows().map((row) => row[0])).toEqual(['a-2']);
    expect(view.button('Next')?.disabled).toBe(true);
  });

  it('restarts at the first page with the total when the page token is refused', async () => {
    await loadAs(harness, platformAdmin);
    const view = render();
    listRequest().flush(list(['a-1'], { total: 2, nextCursor: 'c2' }));
    listRequest().flush({}, { status: 500, statusText: 'Error' });
    view.detect();

    view.button('Next')?.click();
    listRequest().flush(
      { type: 'https://tools.ietf.org/html/rfc9110#section-15.5.21', title: 'Unprocessable', status: 422, code: 'invalid-cursor' },
      { status: 422, statusText: 'Unprocessable' },
    );

    expect(params(listRequest())).toEqual({ size: '25', includeTotal: 'true' });
  });

  it('shows the error state when a page change fails', async () => {
    await loadAs(harness, platformAdmin);
    const view = render();
    listRequest().flush(list(['a-1'], { total: 2, nextCursor: 'c2' }));
    listRequest().flush({}, { status: 500, statusText: 'Error' });
    view.detect();

    view.button('Next')?.click();
    listRequest().flush({}, { status: 500, statusText: 'Error' });
    view.detect();

    expect(view.element.textContent).toContain('Tenants could not be loaded.');
  });

  describe('search', () => {
    function search(view: ReturnType<typeof render>) {
      const input = () => view.element.querySelector<HTMLInputElement>('input[data-filter="q"]')!;
      const type = (text: string) => {
        input().value = text;
        input().dispatchEvent(new Event('input'));
        view.detect();
      };
      const enter = () => {
        input().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
        view.detect();
      };
      const press = () => {
        view.element.querySelector<HTMLButtonElement>('button[aria-label="Search tenants"]')?.click();
        view.detect();
      };
      return { input, type, enter, press };
    }

    async function loaded() {
      await loadAs(harness, platformAdmin);
      const view = render();
      listRequest().flush(list(['acme', 'globex'], { total: 2 }));
      view.detect();
      return { view, box: search(view) };
    }

    it('sends nothing while typing', async () => {
      const { box } = await loaded();

      box.type('acm');

      harness.http.expectNone((request) => request.url === TenantsApi.url);
    });

    it('searches the trimmed text with the total on Enter and on the search button', async () => {
      const { view, box } = await loaded();

      box.type('  Acme ');
      box.enter();
      const byEnter = listRequest();
      expect(params(byEnter)).toEqual({ size: '25', q: 'Acme', includeTotal: 'true' });
      byEnter.flush(list(['acme'], { total: 1 }));
      view.detect();
      expect(view.range()).toBe('1–1 of 1 tenants');

      box.type('glo');
      box.press();
      expect(params(listRequest())).toEqual({ size: '25', q: 'glo', includeTotal: 'true' });
    });

    it('searches without a filter when the text is empty', async () => {
      const { box } = await loaded();

      box.type('   ');
      box.enter();

      expect(params(listRequest())).toEqual({ size: '25', includeTotal: 'true' });
    });

    it('says when nothing matches and clears the search', async () => {
      const { view, box } = await loaded();
      box.type('zzz');
      box.enter();
      listRequest().flush(list([], { total: 0 }));
      view.detect();

      expect(view.element.textContent).toContain('No tenants match this search.');
      expect(view.element.textContent).not.toContain('No tenants yet.');
      view.button('Clear search')?.click();
      view.detect();

      expect(params(listRequest())).toEqual({ size: '25', includeTotal: 'true' });
      expect(box.input().value).toBe('');
    });

    it('keeps the search when paging and when the size changes', async () => {
      const { view, box } = await loaded();
      box.type('a');
      box.enter();
      listRequest().flush(list(['acme'], { total: 30, nextCursor: 'c2' }));
      expect(params(listRequest())).toEqual({ size: '25', q: 'a', after: 'c2' });
      view.detect();

      await view.resize('10');

      expect(params(listRequest())).toEqual({ size: '10', q: 'a', includeTotal: 'true' });
    });

    it('accepts at most 200 characters', async () => {
      const { box } = await loaded();

      expect(box.input().maxLength).toBe(200);
    });
  });

  it('says when there are no tenants', async () => {
    await loadAs(harness, platformAdmin);
    const view = render();
    listRequest().flush(list([], { total: 0 }));
    view.detect();

    expect(view.element.textContent).toContain('No tenants yet.');
  });

  it('offers to try again when loading fails', async () => {
    await loadAs(harness, platformAdmin);
    const view = render();
    listRequest().flush({}, { status: 500, statusText: 'Error' });
    view.detect();

    expect(view.element.textContent).toContain('Tenants could not be loaded.');
    view.button('Try again')?.click();
    expect(params(listRequest())).toEqual({ size: '25', includeTotal: 'true' });
  });

  it.each([
    [true, true],
    [false, false],
  ])('shows Create tenant only when allowed (canCreate %s)', async (canCreate, shown) => {
    await loadAs(harness, platformAdmin);
    const view = render();
    listRequest().flush(list(['acme'], { actions: { canCreate } }));
    view.detect();

    expect(view.button('Create tenant') !== undefined).toBe(shown);
  });

  it('reloads and confirms after a tenant is created', async () => {
    await loadAs(harness, platformAdmin);
    const open = vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({ afterClosed: () => of('acme') } as never);
    const snack = vi.spyOn(TestBed.inject(MatSnackBar), 'open');
    const view = render();
    listRequest().flush(list(['acme'], { total: 1 }));
    view.detect();

    view.button('Create tenant')?.click();

    expect(open).toHaveBeenCalled();
    expect(params(listRequest())).toEqual({ size: '25', includeTotal: 'true' });
    expect(snack).toHaveBeenCalledWith('Tenant acme created.', undefined, expect.objectContaining({ duration: 5000 }));
  });

  it('keeps the search and the size when it restarts after a tenant is created', async () => {
    await loadAs(harness, platformAdmin);
    vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({ afterClosed: () => of('acme') } as never);
    const view = render();
    listRequest().flush(list(['acme'], { total: 1 }));
    view.detect();
    await view.resize('10');
    listRequest().flush(list(['acme'], { total: 1 }));
    view.detect();
    const input = view.element.querySelector<HTMLInputElement>('input[data-filter="q"]')!;
    input.value = 'ac';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    listRequest().flush(list(['acme'], { total: 1 }));
    view.detect();

    view.button('Create tenant')?.click();

    expect(params(listRequest())).toEqual({ size: '10', q: 'ac', includeTotal: 'true' });
  });

  it('does not reload when the dialog is cancelled', async () => {
    await loadAs(harness, platformAdmin);
    vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({ afterClosed: () => of(undefined) } as never);
    const view = render();
    listRequest().flush(list(['acme'], { total: 1 }));
    view.detect();

    view.button('Create tenant')?.click();

    harness.http.expectNone((request) => request.url === TenantsApi.url);
  });
});
