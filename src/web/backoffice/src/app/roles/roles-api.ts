import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Permission {
  name: string;
  displayName: string;
}

export interface Role {
  name: string;
  displayName: string;
  permissions: Permission[];
}

export interface RoleList {
  roles: Role[];
  canAssign: boolean;
}

@Injectable({ providedIn: 'root' })
export class RolesApi {
  static readonly url = '/api/identity/roles';

  private readonly http = inject(HttpClient);

  list(): Observable<RoleList> {
    return this.http.get<RoleList>(RolesApi.url);
  }
}
