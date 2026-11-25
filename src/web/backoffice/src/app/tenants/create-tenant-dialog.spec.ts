import { TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { CreateTenantDialog } from './create-tenant-dialog';
import { TenantsApi } from './tenants-api';

const platformAdmin = { ...staffUser, userType: 'platform' as const, tenantId: null };

describe('CreateTenantDialog', () => {
  let harness: SessionHarness;
  let closed: unknown[];

  beforeEach(async () => {
    closed = [];
    TestBed.overrideProvider(MatDialogRef, { useValue: { close: (value: unknown) => closed.push(value), disableClose: false } });
    harness = setUpSession();
    await loadAs(harness, platformAdmin);
  });

  afterEach(() => harness.http.verify());

  function render() {
    const fixture = TestBed.createComponent(CreateTenantDialog);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const input = (name: string) => element.querySelector<HTMLInputElement>(`input[data-field="${name}"]`)!;
    const type = (name: string, value: string) => {
      const field = input(name);
      field.value = value;
      field.dispatchEvent(new Event('input'));
      field.dispatchEvent(new Event('blur'));
    };
    const fill = (values: Partial<Record<'slug' | 'name' | 'adminEmail' | 'adminPassword', string>>) => {
      const all = { slug: 'acme', name: 'Acme Ltd', adminEmail: 'admin@acme.test', adminPassword: 'Secret#123', ...values };
      for (const [name, value] of Object.entries(all)) {
        type(name, value);
      }
      fixture.detectChanges();
    };
    const createButton = () =>
      Array.from(element.querySelectorAll<HTMLButtonElement>('button')).find((button) => button.textContent?.includes('Create'))!;
    const settle = async () => {
      for (let tick = 0; tick < 5; tick++) {
        fixture.detectChanges();
        await fixture.whenStable();
        await new Promise((resolve) => setTimeout(resolve, 0));
      }

      fixture.detectChanges();
    };
    const errorsUnder = (name: string) =>
      Array.from(input(name).closest('mat-form-field')?.querySelectorAll('mat-error') ?? []).map((error) => error.textContent?.trim());
    const topError = () => element.querySelector('[role="alert"]')?.textContent?.trim();
    return { element, type, fill, createButton, settle, errorsUnder, topError, detect: () => fixture.detectChanges() };
  }

  it.each([
    ['slug', 'Acme Inc', 'Use 3–63 lowercase letters, digits or single hyphens.'],
    ['slug', '', 'Required.'],
    ['name', '', 'Required.'],
    ['name', 'x'.repeat(201), 'At most 200 characters.'],
    ['adminEmail', 'not-an-email', 'Enter a valid email address.'],
    ['adminEmail', 'a'.repeat(250) + '@x.test', 'At most 256 characters.'],
    ['adminPassword', 'short', 'At least 8 characters.'],
  ])('checks %s = %j before sending', async (field, value, message) => {
    const view = render();
    view.fill({ [field]: value });
    await view.settle();

    expect(view.errorsUnder(field)).toContain(message);
    expect(view.createButton().disabled).toBe(true);
  });

  it('accepts an uppercase identifier and sends it lower-cased, with name and email trimmed', async () => {
    const view = render();
    view.fill({ slug: ' Acme ', name: '  Acme Ltd  ', adminEmail: ' admin@acme.test ' });
    await view.settle();
    view.createButton().click();
    await view.settle();

    const request = harness.http.expectOne(TenantsApi.url);
    expect(request.request.body).toEqual({ slug: 'acme', name: 'Acme Ltd', adminEmail: 'admin@acme.test', adminPassword: 'Secret#123' });
    request.flush({ id: 't', slug: 'acme', adminUserId: 'u' }, { status: 201, statusText: 'Created' });
    await view.settle();

    expect(closed).toEqual(['acme']);
  });

  it('shows the service field errors under their fields and unknown ones at the top', async () => {
    const view = render();
    view.fill({});
    view.createButton().click();
    await view.settle();

    harness.http
      .expectOne(TenantsApi.url)
      .flush(
        { type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1', code: 'validation-failed', errors: { slug: ['Slug is reserved.'], adminPassword: ['Needs a digit.', 'Needs a symbol.'], other: ['Odd.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await view.settle();

    expect(view.errorsUnder('slug')).toContain('Slug is reserved.');
    expect(view.errorsUnder('adminPassword')).toContain('Needs a digit. Needs a symbol.');
    expect(view.topError()).toContain('Odd.');
    expect(closed).toEqual([]);
  });

  it.each([
    ['tenant-slug-taken', 'slug', 'A tenant with this identifier already exists.'],
    ['email-taken', 'adminEmail', 'This email is already in use.'],
  ])('puts the %s conflict on its field', async (code, field, message) => {
    const view = render();
    view.fill({});
    view.createButton().click();
    await view.settle();

    harness.http.expectOne(TenantsApi.url).flush({ type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10', code }, { status: 409, statusText: 'Conflict' });
    await view.settle();

    expect(view.errorsUnder(field)).toContain(message);
  });

  it('shows other failures at the top and keeps the values', async () => {
    const view = render();
    view.fill({});
    view.createButton().click();
    await view.settle();

    harness.http.expectOne(TenantsApi.url).flush({ type: 'https://tools.ietf.org/html/rfc9110#section-15.6.1', title: 'An error occurred.' }, { status: 500, statusText: 'Error' });
    await view.settle();

    expect(view.topError()).toBe('The tenant could not be created. Please try again.');
    expect(view.element.querySelector<HTMLInputElement>('input[data-field="slug"]')?.value).toBe('acme');
    expect(closed).toEqual([]);
  });

  it('sends nothing more while saving', async () => {
    const view = render();
    view.fill({});
    view.createButton().click();
    await view.settle();

    expect(view.createButton().disabled).toBe(true);
    view.createButton().click();
    await view.settle();

    harness.http.expectOne(TenantsApi.url).flush({ id: 't', slug: 'acme', adminUserId: 'u' }, { status: 201, statusText: 'Created' });
    await view.settle();
  });

  it('clears a service error when the field is edited', async () => {
    const view = render();
    view.fill({});
    view.createButton().click();
    await view.settle();
    harness.http.expectOne(TenantsApi.url).flush({ type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10', code: 'tenant-slug-taken' }, { status: 409, statusText: 'Conflict' });
    await view.settle();
    expect(view.errorsUnder('slug')).toContain('A tenant with this identifier already exists.');

    view.type('slug', 'acme-two');
    await view.settle();

    expect(view.errorsUnder('slug')).toEqual([]);
  });

  it('offers to retry when the sign-in service is unavailable, and retries the save', async () => {
    const view = render();
    view.fill({});
    view.createButton().click();
    await view.settle();
    harness.http.expectOne(TenantsApi.url).flush({ type: 'identity-unavailable' }, { status: 503, statusText: 'Unavailable' });
    await view.settle();

    const banner = view.element.querySelector('[role="status"]');
    expect(banner?.textContent).toContain('Sign-in service is unavailable.');
    banner?.querySelector('button')?.click();
    await view.settle();

    harness.http.expectOne(TenantsApi.url).flush({ id: 't', slug: 'acme', adminUserId: 'u' }, { status: 201, statusText: 'Created' });
    await view.settle();
    expect(closed).toEqual(['acme']);
  });
});
