import { TestBed } from '@angular/core/testing';
import { SessionHarness, customer, loadAs, setUpSession } from '../session/testing/session-harness';
import { Account } from './account';

describe('Account', () => {
  let harness: SessionHarness;

  afterEach(() => harness.http.verify());

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(Account);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the email and the shop when signed in', async () => {
    harness = setUpSession('/shop/demo/account');
    await loadAs(harness, customer);
    const element = render();

    expect(element.querySelector('h1')?.textContent).toContain('Your account');
    expect(element.textContent).toContain('Email: shopper@mail.test');
    expect(element.textContent).toContain('Shop: demo');
  });

  it('asks a visitor to sign in and returns to the account page', async () => {
    harness = setUpSession('/shop/demo/account');
    await loadAs(harness, null);
    const element = render();

    expect(element.textContent).toContain('Sign in to see your account.');
    element.querySelector('button')?.click();
    expect(harness.location.assigned).toEqual(['/bff/shop/demo/login?returnUrl=' + encodeURIComponent('/shop/demo/account')]);
  });
});
