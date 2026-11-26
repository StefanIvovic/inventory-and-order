import { TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { SessionHarness, loadAs, setUpSession, staffUser } from '../session/testing/session-harness';
import { AddStaffDialog } from './add-staff-dialog';
import { UsersApi } from './users-api';

describe('AddStaffDialog', () => {
  let harness: SessionHarness;
  let closed: unknown[];

  beforeEach(async () => {
    closed = [];
    TestBed.overrideProvider(MatDialogRef, { useValue: { close: (value: unknown) => closed.push(value), disableClose: false } });
    harness = setUpSession();
    await loadAs(harness, staffUser);
  });

  afterEach(() => harness.http.verify());

  function render() {
    const fixture = TestBed.createComponent(AddStaffDialog);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const input = (name: string) => element.querySelector<HTMLInputElement>(`input[data-field="${name}"]`)!;
    const type = (name: string, value: string) => {
      const field = input(name);
      field.value = value;
      field.dispatchEvent(new Event('input'));
      field.dispatchEvent(new Event('blur'));
    };
    const fill = (values: Partial<Record<'email' | 'password', string>>) => {
      for (const [name, value] of Object.entries({ email: 'clerk@demo.test', password: 'Secret#123', ...values })) {
        type(name, value);
      }
      fixture.detectChanges();
    };
    const addButton = () => Array.from(element.querySelectorAll<HTMLButtonElement>('button')).find((button) => button.textContent?.trim() === 'Add')!;
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
    return { element, type, fill, addButton, settle, errorsUnder, topError };
  }

  it.each([
    ['email', '', 'Required.'],
    ['email', 'not-an-email', 'Enter a valid email address.'],
    ['email', 'a'.repeat(250) + '@x.test', 'At most 256 characters.'],
    ['password', '', 'Required.'],
    ['password', 'short', 'At least 8 characters.'],
  ])('checks %s = %j before sending', async (field, value, message) => {
    const view = render();
    view.fill({ [field]: value });
    await view.settle();

    expect(view.errorsUnder(field)).toContain(message);
    expect(view.addButton().disabled).toBe(true);
  });

  it('sends the trimmed email and closes with it', async () => {
    const view = render();
    view.fill({ email: '  clerk@demo.test ' });
    await view.settle();
    view.addButton().click();
    await view.settle();

    const request = harness.http.expectOne(UsersApi.url);
    expect(request.request.body).toEqual({ email: 'clerk@demo.test', password: 'Secret#123' });
    request.flush({ id: 'u-9' }, { status: 201, statusText: 'Created' });
    await view.settle();

    expect(closed).toEqual(['clerk@demo.test']);
  });

  it('shows the service field errors under their fields and unknown ones at the top', async () => {
    const view = render();
    view.fill({});
    view.addButton().click();
    await view.settle();

    harness.http.expectOne(UsersApi.url).flush(
      {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        code: 'validation-failed',
        errors: { password: ['Passwords must have at least one digit.'], other: ['Odd.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await view.settle();

    expect(view.errorsUnder('password')).toContain('Passwords must have at least one digit.');
    expect(view.topError()).toContain('Odd.');
    expect(closed).toEqual([]);
  });

  it('puts email-taken on the email field', async () => {
    const view = render();
    view.fill({});
    view.addButton().click();
    await view.settle();

    harness.http
      .expectOne(UsersApi.url)
      .flush({ type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10', title: 'Conflict', status: 409, code: 'email-taken' }, { status: 409, statusText: 'Conflict' });
    await view.settle();

    expect(view.errorsUnder('email')).toContain('This email is already in use.');
  });

  it('shows other failures at the top and keeps the values', async () => {
    const view = render();
    view.fill({});
    view.addButton().click();
    await view.settle();

    harness.http.expectOne(UsersApi.url).flush({ type: 'https://tools.ietf.org/html/rfc9110#section-15.6.1', title: 'An error occurred.', status: 500 }, { status: 500, statusText: 'Error' });
    await view.settle();

    expect(view.topError()).toBe('The staff user could not be added. Please try again.');
    expect(view.element.querySelector<HTMLInputElement>('input[data-field="email"]')?.value).toBe('clerk@demo.test');
    expect(closed).toEqual([]);
  });

  it('sends nothing more while saving', async () => {
    const view = render();
    view.fill({});
    view.addButton().click();
    await view.settle();

    expect(view.addButton().disabled).toBe(true);
    view.addButton().click();
    await view.settle();

    harness.http.expectOne(UsersApi.url).flush({ id: 'u-9' }, { status: 201, statusText: 'Created' });
    await view.settle();
  });

  it('offers to retry when the sign-in service is unavailable, and retries the save', async () => {
    const view = render();
    view.fill({});
    view.addButton().click();
    await view.settle();
    harness.http.expectOne(UsersApi.url).flush({ type: 'identity-unavailable' }, { status: 503, statusText: 'Unavailable' });
    await view.settle();

    const banner = view.element.querySelector('[role="status"]');
    expect(banner?.textContent).toContain('Sign-in service is unavailable.');
    banner?.querySelector('button')?.click();
    await view.settle();

    harness.http.expectOne(UsersApi.url).flush({ id: 'u-9' }, { status: 201, statusText: 'Created' });
    await view.settle();
    expect(closed).toEqual(['clerk@demo.test']);
  });
});
