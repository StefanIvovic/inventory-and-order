import { InjectionToken } from '@angular/core';

export interface BrowserLocation {
  path(): string;
  search(): string;
  assign(url: string): void;
  replaceSearch(search: string): void;
}

export const BROWSER_LOCATION = new InjectionToken<BrowserLocation>('BROWSER_LOCATION', {
  providedIn: 'root',
  factory: () => ({
    path: () => window.location.pathname,
    search: () => window.location.search,
    assign: (url: string) => window.location.assign(url),
    replaceSearch: (search: string) =>
      window.history.replaceState(window.history.state, '', window.location.pathname + search + window.location.hash),
  }),
});
