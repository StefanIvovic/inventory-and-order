import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

export interface LookupValue {
  id: string;
  code: string;
  name: string;
}

interface LookupValuePage {
  items: LookupValue[];
  nextCursor: string | null;
}

@Injectable({ providedIn: 'root' })
export class LookupsApi {
  static readonly url = '/api/identity/lookups';
  static readonly maxValues = 200;

  private readonly http = inject(HttpClient);

  values(typeCode: string): Observable<LookupValue[]> {
    return this.http
      .get<LookupValuePage>(LookupsApi.valuesUrl(typeCode), { params: { size: LookupsApi.maxValues } })
      .pipe(map((page) => page.items));
  }

  static valuesUrl(typeCode: string): string {
    return `${LookupsApi.url}/${encodeURIComponent(typeCode)}/values`;
  }
}
