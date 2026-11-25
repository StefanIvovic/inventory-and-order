import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { SessionService } from '../session/session.service';
import { signInErrorMessage } from '../session/sign-in-errors';

@Component({
  selector: 'app-sign-in',
  imports: [MatCardModule, MatButtonModule],
  template: `
    <main class="center">
      <mat-card appearance="outlined">
        <mat-card-header>
          <mat-card-title>Backoffice</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <p>Sign in to continue.</p>
          @if (error(); as message) {
            <p class="error" role="alert">{{ message }}</p>
          }
        </mat-card-content>
        <mat-card-actions>
          <button mat-flat-button type="button" (click)="session.signIn()">Sign in</button>
        </mat-card-actions>
      </mat-card>
    </main>
  `,
  styles: `
    .center {
      display: grid;
      place-items: center;
      min-height: 100vh;
    }
    mat-card {
      width: min(24rem, 90vw);
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class SignIn {
  protected readonly session = inject(SessionService);

  protected readonly error = computed(() => {
    const state = this.session.state();
    return state.kind === 'signed-out' && state.error ? signInErrorMessage(state.error) : null;
  });
}
