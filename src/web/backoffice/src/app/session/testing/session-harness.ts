import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiInterceptor } from '../api.interceptor';
import { BROWSER_LOCATION } from '../browser-location';
import { SessionService, SessionUser } from '../session.service';
import { FakeBrowserLocation } from './fake-browser-location';

export const staffUser: SessionUser = {
  userId: 'u-1',
  userType: 'staff',
  tenantId: 't-1',
  email: 'admin@demo.local',
  logoutUrl: '/bff/backoffice/logout?sid=abc',
};

export interface SessionHarness {
  location: FakeBrowserLocation;
  http: HttpTestingController;
  session: SessionService;
}

export function setUpSession(): SessionHarness {
  const location = new FakeBrowserLocation();
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(withInterceptors([apiInterceptor])),
      provideHttpClientTesting(),
      { provide: BROWSER_LOCATION, useValue: location },
    ],
  });
  return { location, http: TestBed.inject(HttpTestingController), session: TestBed.inject(SessionService) };
}

export async function loadAs(harness: SessionHarness, user: SessionUser | null, status = 401): Promise<void> {
  const loaded = harness.session.load();
  const request = harness.http.expectOne(SessionService.userUrl);
  if (user) {
    request.flush(user);
  } else {
    request.flush({}, { status, statusText: 'Error' });
  }

  await loaded;
}
