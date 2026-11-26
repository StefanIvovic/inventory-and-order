import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { RolesApi } from './roles-api';
import { tenantRoles } from './testing/tenant-roles';

describe('RolesApi', () => {
  let http: HttpTestingController;
  let api: RolesApi;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(RolesApi);
  });

  afterEach(() => http.verify());

  it('lists the roles with their display names', async () => {
    const answer = firstValueFrom(api.list());

    const request = http.expectOne('/api/identity/roles');
    expect(request.request.method).toBe('GET');
    request.flush(tenantRoles);

    expect(await answer).toEqual(tenantRoles);
  });
});
