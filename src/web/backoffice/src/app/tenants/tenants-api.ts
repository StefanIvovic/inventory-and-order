import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RequestOptions, contextFor } from '../loading/loading.interceptor';

export interface TenantItem {
  id: string;
  slug: string;
  name: string;
  createdAt: string;
}

export interface TenantList {
  items: TenantItem[];
  nextCursor: string | null;
  total?: number;
  actions: { canCreate: boolean };
}

export interface TenantListRequest {
  q: string;
  size: number;
  after: string | null;
  includeTotal: boolean;
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

  list(request: TenantListRequest, options: RequestOptions = {}): Observable<TenantList> {
    let params = new HttpParams().set('size', request.size);
    const q = request.q.trim();
    if (q !== '') {
      params = params.set('q', q);
    }

    if (request.after) {
      params = params.set('after', request.after);
    }

    if (request.includeTotal) {
      params = params.set('includeTotal', true);
    }

    return this.http.get<TenantList>(TenantsApi.url, { params, context: contextFor(options) });
  }

  create(tenant: NewTenant): Observable<CreatedTenant> {
    return this.http.post<CreatedTenant>(TenantsApi.url, tenant);
  }
}
