import { Component, inject } from '@angular/core';
import { SessionService } from '../session/session.service';

@Component({
  selector: 'app-home',
  template: `<h1>Welcome to {{ session.shop }}.</h1>`,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
  `,
})
export class Home {
  protected readonly session = inject(SessionService);
}
