import { Component } from '@angular/core';

@Component({
  selector: 'app-home',
  template: `<h1>Welcome to the backoffice.</h1>`,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
  `,
})
export class Home {}
