import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RequestOptions, contextFor } from '../loading/loading.interceptor';

export interface StaffItem {
  id: string;
  email: string;
  userType: string;
  disabled: boolean;
  roles: string[];
  createdAt: string;
  actions: { canDisable: boolean; canAssignRoles: boolean };
}

export interface StaffList {
  items: StaffItem[];
  nextCursor: string | null;
  total?: number;
  actions: { canCreate: boolean };
}

export interface StaffFilters {
  q: string;
  status: string | null;
  role: string | null;
}

export interface StaffListRequest extends StaffFilters {
  size: number;
  after: string | null;
  includeTotal: boolean;
}

export interface NewStaffUser {
  email: string;
  password: string;
}

@Injectable({ providedIn: 'root' })
export class UsersApi {
  static readonly url = '/api/identity/users';

  private readonly http = inject(HttpClient);

  list(request: StaffListRequest, options: RequestOptions = {}): Observable<StaffList> {
    let params = new HttpParams().set('size', request.size);
    const q = request.q.trim();
    if (q !== '') {
      params = params.set('q', q);
    }

    if (request.status) {
      params = params.set('status', request.status);
    }

    if (request.role) {
      params = params.set('role', request.role);
    }

    if (request.after) {
      params = params.set('after', request.after);
    }

    if (request.includeTotal) {
      params = params.set('includeTotal', true);
    }

    return this.http.get<StaffList>(UsersApi.url, { params, context: contextFor(options) });
  }

  create(user: NewStaffUser): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(UsersApi.url, user);
  }

  disable(id: string): Observable<void> {
    return this.http.post<void>(`${UsersApi.url}/${encodeURIComponent(id)}/disable`, null);
  }

  assign(id: string, role: string): Observable<void> {
    return this.http.put<void>(UsersApi.roleUrl(id, role), null);
  }

  remove(id: string, role: string): Observable<void> {
    return this.http.delete<void>(UsersApi.roleUrl(id, role));
  }

  static roleUrl(id: string, role: string): string {
    return `${UsersApi.url}/${encodeURIComponent(id)}/roles/${encodeURIComponent(role)}`;
  }
}
