import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { SessionService } from '../session/session.service';

@Component({
  selector: 'app-account',
  imports: [MatButtonModule],
  template: `
    @if (user(); as current) {
      <h1>Your account</h1>
      <p>Email: {{ current.email ?? current.userId }}</p>
      <p>Shop: {{ session.shop }}</p>
    } @else {
      <p>Sign in to see your account.</p>
      <button mat-flat-button type="button" (click)="session.signIn()">Sign in</button>
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
  `,
})
export class Account {
  protected readonly session = inject(SessionService);

  protected readonly user = computed(() => {
    const state = this.session.state();
    return state.kind === 'signed-in' ? state.user : null;
  });
}
