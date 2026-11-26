import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

export interface LookupValue {
  id: string;
  code: string;
  name: string;
}

export interface LookupValuePage {
  items: LookupValue[];
  nextCursor: string | null;
}

export interface LookupPageRequest {
  q?: string;
  ids?: readonly string[];
  codes?: readonly string[];
  size: number;
  after?: string | null;
}

export const identityService = '/api/identity';

@Injectable({ providedIn: 'root' })
export class LookupsApi {
  static readonly url = `${identityService}/lookups`;
  static readonly maxValues = 200;

  private readonly http = inject(HttpClient);

  values(typeCode: string): Observable<LookupValue[]> {
    return this.page(typeCode, { size: LookupsApi.maxValues }).pipe(map((page) => page.items));
  }

  page(typeCode: string, request: LookupPageRequest, service = identityService): Observable<LookupValuePage> {
    let params = new HttpParams().set('size', request.size);
    const q = request.q?.trim();
    if (q) {
      params = params.set('q', q);
    }

    for (const id of request.ids ?? []) {
      params = params.append('ids', id);
    }

    for (const code of request.codes ?? []) {
      params = params.append('codes', code);
    }

    if (request.after) {
      params = params.set('after', request.after);
    }

    return this.http.get<LookupValuePage>(LookupsApi.valuesUrl(typeCode, service), { params });
  }

  static valuesUrl(typeCode: string, service = identityService): string {
    return `${service}/lookups/${encodeURIComponent(typeCode)}/values`;
  }
}
