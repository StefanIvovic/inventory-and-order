import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiInterceptor } from '../api.interceptor';
import { BROWSER_LOCATION } from '../browser-location';
import { SessionService, SessionUser } from '../session.service';
import { FakeBrowserLocation } from './fake-browser-location';

export const customer: SessionUser = {
  userId: 'c-1',
  userType: 'customer',
  tenantId: 't-1',
  email: 'shopper@mail.test',
  logoutUrl: '/bff/shop/demo/logout?sid=abc',
  slug: 'demo',
};

export interface SessionHarness {
  location: FakeBrowserLocation;
  http: HttpTestingController;
  session: () => SessionService;
}

export function setUpSession(path = '/shop/demo/', search = ''): SessionHarness {
  const location = new FakeBrowserLocation();
  location.currentPath = path;
  location.currentSearch = search;
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(withInterceptors([apiInterceptor])),
      provideHttpClientTesting(),
      { provide: BROWSER_LOCATION, useValue: location },
    ],
  });
  return { location, http: TestBed.inject(HttpTestingController), session: () => TestBed.inject(SessionService) };
}

export async function loadAs(harness: SessionHarness, user: SessionUser | null, status = 401): Promise<void> {
  const session = harness.session();
  const loaded = session.load();
  const request = harness.http.expectOne(session.userUrl);
  if (user) {
    request.flush(user);
  } else {
    request.flush({}, { status, statusText: 'Error' });
  }

  await loaded;
}
