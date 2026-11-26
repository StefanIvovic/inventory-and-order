import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormField, form, required } from '@angular/forms/signals';
import { TestRequest } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { LookupSelect, LookupSelectValue } from './lookup-select';
import { LookupValue, LookupsApi } from './lookups-api';

const statuses: LookupValue[] = [
  { id: 'id-active', code: 'active', name: 'Active' },
  { id: 'id-disabled', code: 'disabled', name: 'Disabled' },
];

@Component({
  imports: [LookupSelect],
  template: `<app-lookup-select
    typeCode="staff-status"
    label="Status"
    [multiple]="multiple()"
    [valueBy]="valueBy()"
    [emptyLabel]="emptyLabel()"
    [disabled]="disabled()"
    [readonly]="readonly()"
    [required]="isRequired()"
    [(value)]="value"
  />`,
})
class PlainHost {
  readonly multiple = signal(false);
  readonly valueBy = signal<'code' | 'id'>('code');
  readonly emptyLabel = signal<string | null>(null);
  readonly disabled = signal(false);
  readonly readonly = signal(false);
  readonly isRequired = signal(false);
  readonly value = signal<LookupSelectValue>(null);
}

@Component({
  imports: [LookupSelect, FormField],
  template: `<app-lookup-select typeCode="staff-status" label="Status" [formField]="f.status" />
    <app-lookup-select typeCode="user-type" label="Types" [multiple]="true" [formField]="f.types" />`,
})
class FormHost {
  readonly model = signal<{ status: string | null; types: string[] }>({ status: null, types: ['staff'] });
  readonly f = form(this.model, (path) => {
    required(path.status, { message: 'Pick a status.' });
  });
}

describe('LookupSelect', () => {
  let harness: SessionHarness;

  beforeEach(async () => {
    harness = setUpSession();
    await loadAs(harness, staffUser);
  });

  afterEach(() => harness.http.verify());

  async function settle(fixture: { detectChanges: () => void; whenStable: () => Promise<unknown> }) {
    for (let tick = 0; tick < 4; tick++) {
      fixture.detectChanges();
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }

    fixture.detectChanges();
  }

  function pages(typeCode = 'staff-status'): TestRequest[] {
    return harness.http.match((request) => request.url === LookupsApi.valuesUrl(typeCode));
  }

  function page(typeCode = 'staff-status'): TestRequest {
    return harness.http.expectOne((request) => request.url === LookupsApi.valuesUrl(typeCode));
  }

  function params(request: TestRequest): Record<string, string | string[] | null> {
    const all = request.request.params;
    return Object.fromEntries(all.keys().map((key) => [key, all.getAll(key)!.length > 1 ? all.getAll(key) : all.get(key)]));
  }

  async function render(setup: (host: PlainHost) => void = () => undefined) {
    const fixture = TestBed.createComponent(PlainHost);
    setup(fixture.componentInstance);
    await settle(fixture);
    const element = fixture.nativeElement as HTMLElement;
    const input = () => element.querySelector<HTMLInputElement>('input[data-lookup-input]')!;
    const select = element.querySelector('app-lookup-select')!;
    const panel = () => document.getElementById(input().getAttribute('aria-controls') ?? '');
    const options = () => Array.from(panel()?.querySelectorAll('mat-option') ?? []).map((option) => option.textContent?.trim());
    const option = (text: string) =>
      Array.from(panel()?.querySelectorAll<HTMLElement>('mat-option') ?? []).find((candidate) => candidate.textContent?.trim().startsWith(text))!;
    const open = async () => {
      fixture.debugElement.query(By.directive(MatAutocompleteTrigger)).injector.get(MatAutocompleteTrigger).openPanel();
      await settle(fixture);
    };
    const type = async (text: string) => {
      input().value = text;
      input().dispatchEvent(new Event('input'));
      await settle(fixture);
    };
    const key = async (name: string) => {
      input().dispatchEvent(new KeyboardEvent('keydown', { key: name, code: name, keyCode: name === 'Enter' ? 13 : name === 'Escape' ? 27 : name === 'ArrowDown' ? 40 : 8, bubbles: true }));
      await settle(fixture);
    };
    const button = (selector: string) => element.querySelector<HTMLButtonElement>(selector);
    const chips = () => Array.from(element.querySelectorAll('mat-chip-row')).map((chip) => chip.textContent?.replace('×', '').trim());
    return { fixture, host: fixture.componentInstance, element, select, input, panel, options, option, open, type, key, button, chips, settle: () => settle(fixture) };
  }

  describe('single choice', () => {
    it('asks for the first page once when opened and shows the names, with the empty choice first', async () => {
      const view = await render((host) => host.emptyLabel.set('All'));

      await view.open();
      const first = page();
      expect(params(first)).toEqual({ size: '50' });
      first.flush({ items: statuses, nextCursor: null });
      await view.settle();

      expect(view.options()).toEqual(['All', 'Active', 'Disabled']);
      await view.open();
      expect(pages()).toEqual([]);
    });

    it('sends nothing while typing and searches on Enter or the search button', async () => {
      const view = await render();
      await view.open();
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();

      await view.type('  dis ');
      expect(pages()).toEqual([]);

      await view.key('Enter');
      const byEnter = page();
      expect(params(byEnter)).toEqual({ size: '50', q: 'dis' });
      byEnter.flush({ items: [statuses[1]!], nextCursor: null });
      await view.settle();
      expect(view.options()).toEqual(['Disabled']);

      await view.type('');
      view.button('[data-lookup-search]')!.click();
      await view.settle();
      expect(params(page())).toEqual({ size: '50' });
    });

    it('names the loading row for screen readers until the first page arrives', async () => {
      const view = await render();
      await view.open();

      expect(view.panel()?.querySelector('mat-option.state')?.getAttribute('aria-label')).toBe('Loading options');
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();
      expect(view.panel()?.querySelector('mat-option.state')).toBeNull();
    });

    it('says when nothing matches', async () => {
      const view = await render();
      await view.open();
      page().flush({ items: [], nextCursor: null });
      await view.settle();

      expect(view.options()).toEqual(['No matches']);
    });

    it.each([
      ['code', 'disabled'],
      ['id', 'id-disabled'],
    ] as const)('picks a value by %s, closes and shows its name', async (valueBy, expected) => {
      const view = await render((host) => host.valueBy.set(valueBy));
      await view.open();
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();

      view.option('Disabled').click();
      await view.settle();

      expect(view.host.value()).toBe(expected);
      expect(view.input().value).toBe('Disabled');
      expect(view.panel()).toBeNull();
    });

    it('picking the empty choice gives null', async () => {
      const view = await render((host) => {
        host.emptyLabel.set('All');
        host.value.set('active');
      });
      page().flush({ items: [statuses[0]!], nextCursor: null });
      await view.open();
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();

      view.option('All').click();
      await view.settle();

      expect(view.host.value()).toBeNull();
    });

    it.each([
      ['code', 'codes', 'disabled'],
      ['id', 'ids', 'id-disabled'],
    ] as const)('shows a preset value by its name after one %s request', async (valueBy, parameter, preset) => {
      const view = await render((host) => {
        host.valueBy.set(valueBy);
        host.value.set(preset);
      });

      const names = page();
      expect(params(names)).toEqual({ size: '1', [parameter]: preset });
      names.flush({ items: [statuses[1]!], nextCursor: null });
      await view.settle();

      expect(view.input().value).toBe('Disabled');
    });

    it.each([
      ['missing from the answer', (request: TestRequest) => request.flush({ items: [], nextCursor: null })],
      ['failing', (request: TestRequest) => request.flush({}, { status: 500, statusText: 'Error' })],
    ])('shows Unavailable for a preset value %s, never the code', async (_, answer) => {
      const view = await render((host) => host.value.set('gone'));

      answer(page());
      await view.settle();

      expect(view.input().value).toBe('Unavailable');
    });

    it('offers to try again when the options cannot be loaded', async () => {
      const view = await render();
      await view.open();
      page().flush({}, { status: 500, statusText: 'Error' });
      await view.settle();

      expect(view.options()).toEqual(['Options could not be loaded.', 'Try again']);
      view.option('Try again').click();
      await view.settle();
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();

      expect(view.options()).toEqual(['Active', 'Disabled']);
      expect(view.host.value()).toBeNull();
    });

    it('ignores the answer to an older search', async () => {
      const view = await render();
      await view.open();
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();

      await view.type('act');
      await view.key('Enter');
      await view.type('dis');
      await view.key('Enter');
      const [older, newer] = pages();
      newer!.flush({ items: [statuses[1]!], nextCursor: null });
      older!.flush({ items: [statuses[0]!], nextCursor: null });
      await view.settle();

      expect(view.options()).toEqual(['Disabled']);
    });

    it('clears an optional value with the clear button, and has none when required or with an empty choice', async () => {
      const view = await render((host) => host.value.set('active'));
      page().flush({ items: [statuses[0]!], nextCursor: null });
      await view.settle();

      expect(view.button('[data-lookup-clear]')?.getAttribute('aria-label')).toBe('Clear Status');
      view.button('[data-lookup-clear]')!.click();
      await view.settle();
      expect(view.host.value()).toBeNull();
      for (const opened of pages()) {
        opened.flush({ items: statuses, nextCursor: null });
      }

      view.host.value.set('active');
      view.host.isRequired.set(true);
      await view.settle();
      expect(view.button('[data-lookup-clear]')).toBeNull();

      view.host.isRequired.set(false);
      view.host.emptyLabel.set('All');
      await view.settle();
      expect(view.button('[data-lookup-clear]')).toBeNull();
    });

    it.each([
      ['disabled', (host: PlainHost) => host.disabled.set(true)],
      ['read-only', (host: PlainHost) => host.readonly.set(true)],
    ])('cannot be opened or changed when %s', async (_, lock) => {
      const view = await render((host) => {
        host.value.set('active');
        lock(host);
      });
      page().flush({ items: [statuses[0]!], nextCursor: null });
      await view.settle();

      view.button('[data-lookup-search]')!.click();
      await view.settle();

      expect(view.button('[data-lookup-search]')!.disabled).toBe(true);
      expect(view.input().value).toBe('Active');
      expect(view.panel()).toBeNull();
      expect(pages()).toEqual([]);
    });

    it('works with the keyboard and announces itself', async () => {
      const view = await render();
      expect(view.input().getAttribute('role')).toBe('combobox');
      expect(view.button('[data-lookup-search]')?.getAttribute('aria-label')).toBe('Search Status');
      await view.open();
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();
      expect(view.panel()?.querySelector('mat-option')?.getAttribute('role')).toBe('option');

      await view.key('ArrowDown');
      expect(view.panel()?.querySelector('mat-option.mat-mdc-option-active')?.textContent?.trim()).toBe('Active');
      await view.key('Enter');
      expect(view.host.value()).toBe('active');
      expect(pages().map((request) => request.request.urlWithParams)).toEqual([]);

      await view.open();
      await view.key('Escape');
      expect(view.panel()).toBeNull();
      expect(view.host.value()).toBe('active');
    });
  });

  describe('multiple choice', () => {
    async function renderMultiple(preset: string[] = []) {
      const view = await render((host) => {
        host.multiple.set(true);
        host.value.set(preset);
      });
      return view;
    }

    it('adds chips in order, clears the text, marks chosen values and never duplicates', async () => {
      const view = await renderMultiple();
      await view.open();
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();

      view.option('Disabled').click();
      await view.settle();
      await view.open();
      view.option('Active').click();
      await view.settle();
      await view.open();

      expect(view.host.value()).toEqual(['disabled', 'active']);
      expect(view.chips()).toEqual(['Disabled', 'Active']);
      expect(view.input().value).toBe('');
      expect(view.option('Active').classList.contains('chosen')).toBe(true);

      view.option('Active').click();
      await view.settle();
      expect(view.host.value()).toEqual(['disabled', 'active']);
    });

    it('removes a chip with its button or Backspace', async () => {
      const view = await renderMultiple(['active', 'disabled']);
      page().flush({ items: statuses, nextCursor: null });
      await view.settle();

      const remove = view.element.querySelector<HTMLButtonElement>('mat-chip-row[data-key="active"] button')!;
      expect(remove.getAttribute('aria-label')).toBe('Remove Active');
      remove.click();
      await view.settle();
      expect(view.host.value()).toEqual(['disabled']);

      await view.key('Backspace');
      expect(view.host.value()).toEqual([]);
      for (const opened of pages()) {
        opened.flush({ items: statuses, nextCursor: null });
      }
    });

    it('shows preset values as chips after one request, and Unavailable for an unknown one that can be removed', async () => {
      const view = await renderMultiple(['active', 'gone']);

      const names = page();
      expect(params(names)).toEqual({ size: '2', codes: ['active', 'gone'] });
      names.flush({ items: [statuses[0]!], nextCursor: null });
      await view.settle();

      expect(view.chips()).toEqual(['Active', 'Unavailable']);
      view.element.querySelector<HTMLButtonElement>('mat-chip-row[data-key="gone"] button')!.click();
      await view.settle();
      expect(view.host.value()).toEqual(['active']);
      for (const opened of pages()) {
        opened.flush({ items: statuses, nextCursor: null });
      }
    });

    it('asks for the names of 250 preset values in chunks of 200', async () => {
      const preset = Array.from({ length: 250 }, (_, index) => `v-${index}`);
      await renderMultiple(preset);

      const requests = pages();
      expect(requests.map((request) => request.request.params.getAll('codes')?.length)).toEqual([200, 50]);
      for (const request of requests) {
        request.flush({ items: [], nextCursor: null });
      }
    });
  });

  describe('long lists', () => {
    it('loads the next page at the end of the panel, once at a time, and stops after the last', async () => {
      const view = await render();
      await view.open();
      page().flush({ items: [statuses[0]!], nextCursor: 'c2' });
      await view.settle();

      view.panel()!.dispatchEvent(new Event('scroll'));
      view.panel()!.dispatchEvent(new Event('scroll'));
      const next = page();
      expect(params(next)).toEqual({ size: '50', after: 'c2' });
      next.flush({ items: [statuses[1]!], nextCursor: null });
      await view.settle();

      expect(view.options()).toEqual(['Active', 'Disabled']);
      view.panel()!.dispatchEvent(new Event('scroll'));
      expect(pages()).toEqual([]);
    });

    it('restarts at the first page when a new search starts', async () => {
      const view = await render();
      await view.open();
      page().flush({ items: [statuses[0]!], nextCursor: 'c2' });
      await view.settle();

      await view.type('dis');
      await view.key('Enter');
      const search = page();
      expect(params(search)).toEqual({ size: '50', q: 'dis' });
      search.flush({ items: [statuses[1]!], nextCursor: null });
      await view.settle();

      expect(view.options()).toEqual(['Disabled']);
      view.panel()!.dispatchEvent(new Event('scroll'));
      expect(pages()).toEqual([]);
    });
  });

  describe('Signal Forms', () => {
    it('binds single and multiple values and shows the form errors once touched', async () => {
      const fixture = TestBed.createComponent(FormHost);
      await settle(fixture);
      page('user-type').flush({ items: [{ id: 's', code: 'staff', name: 'Staff' }], nextCursor: null });
      await settle(fixture);
      const element = fixture.nativeElement as HTMLElement;
      const [statusInput, typesInput] = Array.from(element.querySelectorAll<HTMLInputElement>('input[data-lookup-input]'));

      expect(element.querySelectorAll('mat-chip-row')[0]?.textContent).toContain('Staff');
      expect(element.textContent).not.toContain('Pick a status.');

      statusInput!.dispatchEvent(new Event('blur'));
      await settle(fixture);
      expect(fixture.componentInstance.f.status().touched()).toBe(true);
      expect(element.textContent).toContain('Pick a status.');

      fixture.debugElement.queryAll(By.directive(MatAutocompleteTrigger))[0]!.injector.get(MatAutocompleteTrigger).openPanel();
      await settle(fixture);
      page().flush({ items: statuses, nextCursor: null });
      await settle(fixture);
      Array.from(document.querySelectorAll<HTMLElement>('mat-option')).find((option) => option.textContent?.includes('Disabled'))!.click();
      await settle(fixture);

      expect(fixture.componentInstance.model()).toEqual({ status: 'disabled', types: ['staff'] });
      expect(element.textContent).not.toContain('Pick a status.');
      expect(typesInput).toBeDefined();
    });
  });
});
