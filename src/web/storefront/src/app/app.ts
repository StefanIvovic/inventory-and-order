import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Frame } from './frame/frame';
import { SessionService } from './session/session.service';
import { ShopNotFound } from './shop-not-found/shop-not-found';

@Component({
  selector: 'app-root',
  imports: [MatProgressBarModule, Frame, ShopNotFound],
  template: `
    @switch (session.state().kind) {
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Loading" />
      }
      @case ('no-shop') {
        <app-shop-not-found />
      }
      @default {
        <app-frame />
      }
    }
  `,
})
export class App {
  protected readonly session = inject(SessionService);
}
