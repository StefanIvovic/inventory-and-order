import { Component, inject } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SessionService } from './session/session.service';
import { Shell } from './shell/shell';
import { SignIn } from './sign-in/sign-in';
import { Unavailable } from './unavailable/unavailable';

@Component({
  selector: 'app-root',
  imports: [MatProgressBarModule, SignIn, Unavailable, Shell],
  template: `
    @switch (session.state().kind) {
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Loading" />
      }
      @case ('signed-out') {
        <app-sign-in />
      }
      @case ('unavailable') {
        <app-unavailable />
      }
      @case ('signed-in') {
        <app-shell />
      }
    }
  `,
})
export class App {
  protected readonly session = inject(SessionService);
}
