import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface TenantItem {
  id: string;
  slug: string;
  name: string;
  createdAt: string;
}

export interface TenantList {
  items: TenantItem[];
  actions: { canCreate: boolean };
}

export interface NewTenant {
  slug: string;
  name: string;
  adminEmail: string;
  adminPassword: string;
}

export interface CreatedTenant {
  id: string;
  slug: string;
  adminUserId: string;
}

@Injectable({ providedIn: 'root' })
export class TenantsApi {
  static readonly url = '/api/identity/tenants';

  private readonly http = inject(HttpClient);

  list(): Observable<TenantList> {
    return this.http.get<TenantList>(TenantsApi.url);
  }

  create(tenant: NewTenant): Observable<CreatedTenant> {
    return this.http.post<CreatedTenant>(TenantsApi.url, tenant);
  }
}
