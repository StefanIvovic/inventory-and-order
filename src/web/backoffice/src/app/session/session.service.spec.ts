import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BROWSER_LOCATION } from './browser-location';
import { SessionService, SessionUser } from './session.service';
import { FakeBrowserLocation } from './testing/fake-browser-location';

const staff: SessionUser = {
  userId: 'u-1',
  userType: 'staff',
  tenantId: 't-1',
  email: 'admin@demo.local',
  logoutUrl: '/bff/backoffice/logout?sid=abc',
};

describe('SessionService', () => {
  let location: FakeBrowserLocation;
  let http: HttpTestingController;
  let session: SessionService;

  function setUp(path = '/backoffice/', search = ''): void {
    location = new FakeBrowserLocation();
    location.currentPath = path;
    location.currentSearch = search;
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: BROWSER_LOCATION, useValue: location }],
    });
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionService);
  }

  afterEach(() => http.verify());

  it('starts loading', () => {
    setUp();
    expect(session.state().kind).toBe('loading');
  });

  it('is signed in when the user endpoint answers 200', async () => {
    setUp();
    const loaded = session.load();
    http.expectOne('/bff/backoffice/user').flush(staff);
    await loaded;
    expect(session.state()).toEqual({ kind: 'signed-in', user: staff });
  });

  it('is signed out when the user endpoint answers 401', async () => {
    setUp();
    const loaded = session.load();
    http.expectOne('/bff/backoffice/user').flush({ type: 'session-required' }, { status: 401, statusText: 'Unauthorized' });
    await loaded;
    expect(session.state()).toEqual({ kind: 'signed-out', error: null });
  });

  it.each([500, 502, 503])('is unavailable when the user endpoint answers %s', async (status) => {
    setUp();
    const loaded = session.load();
    http.expectOne('/bff/backoffice/user').flush('', { status, statusText: 'Error' });
    await loaded;
    expect(session.state().kind).toBe('unavailable');
  });

  it('is unavailable on a network error', async () => {
    setUp();
    const loaded = session.load();
    http.expectOne('/bff/backoffice/user').error(new ProgressEvent('error'));
    await loaded;
    expect(session.state().kind).toBe('unavailable');
  });

  it('reads the sign-in error once and removes it from the address', async () => {
    setUp('/backoffice/', '?signin-error=access_denied&tab=2');
    const loaded = session.load();
    http.expectOne('/bff/backoffice/user').flush({}, { status: 401, statusText: 'Unauthorized' });
    await loaded;
    expect(session.state()).toEqual({ kind: 'signed-out', error: 'access_denied' });
    expect(location.currentSearch).toBe('?tab=2');

    const again = session.load();
    http.expectOne('/bff/backoffice/user').flush({}, { status: 401, statusText: 'Unauthorized' });
    await again;
    expect(session.state()).toEqual({ kind: 'signed-out', error: null });
  });

  it('signs in with the current path and query as the return path', () => {
    setUp('/backoffice/orders/7', '?tab=lines');
    session.signIn();
    expect(location.assigned).toEqual(['/bff/backoffice/login?returnUrl=' + encodeURIComponent('/backoffice/orders/7?tab=lines')]);
  });

  it('never sends the sign-in error back as part of the return path', () => {
    setUp('/backoffice/', '?signin-error=server_error');
    session.signIn();
    expect(location.assigned).toEqual(['/bff/backoffice/login?returnUrl=' + encodeURIComponent('/backoffice/')]);
  });

  it('signs out with the sign-out address from the session', async () => {
    setUp();
    const loaded = session.load();
    http.expectOne('/bff/backoffice/user').flush(staff);
    await loaded;
    session.signOut();
    expect(location.assigned).toEqual(['/bff/backoffice/logout?sid=abc']);
  });

  it('does nothing on sign-out without a session', () => {
    setUp();
    session.signOut();
    expect(location.assigned).toEqual([]);
  });
});
