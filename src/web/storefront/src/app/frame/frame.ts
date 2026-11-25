import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterOutlet } from '@angular/router';
import { SessionService } from '../session/session.service';
import { signInErrorMessage } from '../session/sign-in-errors';

@Component({
  selector: 'app-frame',
  imports: [MatToolbarModule, MatButtonModule, MatMenuModule, RouterOutlet, RouterLink],
  template: `
    <mat-toolbar>
      <a class="title" [routerLink]="['/', session.shop]">{{ session.shop }}</a>
      <span class="spacer"></span>
      @if (user(); as current) {
        <button mat-button type="button" aria-label="Account menu" [matMenuTriggerFor]="menu">
          {{ current.email ?? current.userId }}
        </button>
        <mat-menu #menu="matMenu">
          <a mat-menu-item [routerLink]="['/', session.shop, 'account']">Account</a>
          <button mat-menu-item type="button" (click)="session.signOut()">Sign out</button>
        </mat-menu>
      } @else {
        <button mat-flat-button type="button" (click)="session.signIn()">Sign in</button>
      }
    </mat-toolbar>
    @if (error(); as message) {
      <p class="banner error" role="alert">{{ message }}</p>
    }
    @if (session.state().kind === 'unavailable') {
      <div class="banner" role="status">
        <span>The shop is having trouble right now.</span>
        <button mat-button type="button" (click)="reload()">Try again</button>
      </div>
    }
    @if (session.retryNeeded()) {
      <div class="banner" role="status">
        <span>Sign-in service is unavailable.</span>
        <button mat-button type="button" (click)="session.retry()">Try again</button>
      </div>
    }
    <main class="content">
      <router-outlet />
    </main>
  `,
  styles: `
    .title {
      color: inherit;
      text-decoration: none;
    }
    .spacer {
      flex: 1;
    }
    .banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin: 0;
      padding: 0.5rem 1rem;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .content {
      padding: 1.5rem;
    }
  `,
})
export class Frame {
  protected readonly session = inject(SessionService);

  protected readonly user = computed(() => {
    const state = this.session.state();
    return state.kind === 'signed-in' ? state.user : null;
  });

  protected readonly error = computed(() => {
    const state = this.session.state();
    return state.kind === 'visitor' && state.error ? signInErrorMessage(state.error) : null;
  });

  protected reload(): void {
    void this.session.load();
  }
}
