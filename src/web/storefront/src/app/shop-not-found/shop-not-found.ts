import { Component } from '@angular/core';

@Component({
  selector: 'app-shop-not-found',
  template: `
    <main class="center">
      <h1>This shop doesn't exist.</h1>
    </main>
  `,
  styles: `
    .center {
      display: grid;
      place-items: center;
      min-height: 100vh;
    }
    h1 {
      font: var(--mat-sys-headline-small);
    }
  `,
})
export class ShopNotFound {}
