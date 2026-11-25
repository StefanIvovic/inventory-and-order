import { BrowserLocation } from '../browser-location';

export class FakeBrowserLocation implements BrowserLocation {
  currentPath = '/shop/demo/';
  currentSearch = '';
  readonly assigned: string[] = [];

  path(): string {
    return this.currentPath;
  }

  search(): string {
    return this.currentSearch;
  }

  assign(url: string): void {
    this.assigned.push(url);
  }

  replaceSearch(search: string): void {
    this.currentSearch = search;
  }
}
