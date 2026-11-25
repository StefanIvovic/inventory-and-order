import { SessionHarness, customer, loadAs, setUpSession } from './testing/session-harness';

describe('SessionService', () => {
  let harness: SessionHarness;

  afterEach(() => harness.http.verify());

  it('has no shop and asks nothing for an invalid shop address', async () => {
    harness = setUpSession('/shop/Demo/');
    const session = harness.session();

    await session.load();

    expect(session.state().kind).toBe('no-shop');
    harness.http.expectNone(() => true);
  });

  it('uses the shop from the address for the user endpoint', () => {
    harness = setUpSession('/shop/acme/account');
    const session = harness.session();

    void session.load();

    harness.http.expectOne('/bff/shop/acme/user').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(session.shop).toBe('acme');
  });

  it('is signed in on 200', async () => {
    harness = setUpSession();
    await loadAs(harness, customer);
    expect(harness.session().state()).toEqual({ kind: 'signed-in', user: customer });
  });

  it('is a visitor on 401', async () => {
    harness = setUpSession();
    await loadAs(harness, null);
    expect(harness.session().state()).toEqual({ kind: 'visitor', error: null });
  });

  it.each([500, 502, 503])('is unavailable on %s', async (status) => {
    harness = setUpSession();
    await loadAs(harness, null, status);
    expect(harness.session().state().kind).toBe('unavailable');
  });

  it('is unavailable on a network error', async () => {
    harness = setUpSession();
    const session = harness.session();
    const loaded = session.load();
    harness.http.expectOne('/bff/shop/demo/user').error(new ProgressEvent('error'));
    await loaded;
    expect(session.state().kind).toBe('unavailable');
  });

  it('reads the sign-in error once and removes it from the address', async () => {
    harness = setUpSession('/shop/demo/', '?signin-error=invalid_request&x=1');
    await loadAs(harness, null);
    expect(harness.session().state()).toEqual({ kind: 'visitor', error: 'invalid_request' });
    expect(harness.location.currentSearch).toBe('?x=1');

    await loadAs(harness, null);
    expect(harness.session().state()).toEqual({ kind: 'visitor', error: null });
  });

  it('signs in at the shop login with the current page as return path', () => {
    harness = setUpSession('/shop/demo/account', '?tab=2&signin-error=server_error');
    harness.session().signIn();
    expect(harness.location.assigned).toEqual(['/bff/shop/demo/login?returnUrl=' + encodeURIComponent('/shop/demo/account?tab=2')]);
  });

  it('signs out with the session sign-out address', async () => {
    harness = setUpSession();
    await loadAs(harness, customer);
    harness.session().signOut();
    expect(harness.location.assigned).toEqual(['/bff/shop/demo/logout?sid=abc']);
  });

  it('does nothing on sign-out without a session', () => {
    harness = setUpSession();
    harness.session().signOut();
    expect(harness.location.assigned).toEqual([]);
  });
});
