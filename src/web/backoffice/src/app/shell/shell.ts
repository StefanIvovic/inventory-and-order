import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';
import { SessionService } from '../session/session.service';

const roleLabels = {
  staff: 'Staff',
  platform: 'Platform administrator',
} as const;

@Component({
  selector: 'app-shell',
  imports: [MatToolbarModule, MatButtonModule, MatMenuModule, RouterOutlet],
  template: `
    <mat-toolbar>
      <span class="title">Backoffice</span>
      <span class="spacer"></span>
      @if (user(); as current) {
        <span class="role">{{ role() }}</span>
        <button mat-button type="button" aria-label="User menu" [matMenuTriggerFor]="menu">
          {{ current.email ?? current.userId }}
        </button>
      }
      <mat-menu #menu="matMenu">
        <button mat-menu-item type="button" (click)="session.signOut()">Sign out</button>
      </mat-menu>
    </mat-toolbar>
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
    .spacer {
      flex: 1;
    }
    .role {
      font: var(--mat-sys-label-medium);
      padding: 0.25rem 0.75rem;
      border-radius: 1rem;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      margin-inline-end: 0.5rem;
    }
    .banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.5rem 1rem;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .content {
      padding: 1.5rem;
    }
  `,
})
export class Shell {
  protected readonly session = inject(SessionService);

  protected readonly user = computed(() => {
    const state = this.session.state();
    return state.kind === 'signed-in' ? state.user : null;
  });

  protected readonly role = computed(() => {
    const user = this.user();
    return user ? roleLabels[user.userType] : '';
  });
}
