import { LiveAnnouncer } from '@angular/cdk/a11y';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { LoadingService } from '../loading/loading.service';
import { LookupValue, LookupsApi } from '../lookups/lookups-api';
import { SessionService } from '../session/session.service';

interface NavigationEntry {
  label: string;
  path: string;
}

const homeEntry: NavigationEntry = { label: 'Home', path: '/' };
const tenantsEntry: NavigationEntry = { label: 'Tenants', path: '/tenants' };
const usersEntry: NavigationEntry = { label: 'Users', path: '/users' };
const rolesEntry: NavigationEntry = { label: 'Roles', path: '/roles' };
const userTypeList = 'user-type';

@Component({
  selector: 'app-shell',
  imports: [
    MatToolbarModule,
    MatButtonModule,
    MatMenuModule,
    MatProgressBarModule,
    MatSidenavModule,
    MatListModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
  ],
  template: `
    <mat-toolbar>
      @if (handset()) {
        <button mat-button type="button" aria-label="Menu" (click)="navigation.toggle()">Menu</button>
      }
      <span class="title">Backoffice</span>
      <span class="spacer"></span>
      @if (user(); as current) {
        @if (role(); as name) {
          <span class="role">{{ name }}</span>
        }
        <button mat-button type="button" aria-label="User menu" [matMenuTriggerFor]="menu">
          {{ current.email ?? current.userId }}
        </button>
      }
      <mat-menu #menu="matMenu">
        <button mat-menu-item type="button" (click)="session.signOut()">Sign out</button>
      </mat-menu>
    </mat-toolbar>
    <div class="loading">
      @if (loading.visible()) {
        <mat-progress-bar mode="indeterminate" aria-label="Loading" />
      }
    </div>
    @if (session.retryNeeded()) {
      <div class="banner" role="status">
        <span>Sign-in service is unavailable.</span>
        <button mat-button type="button" (click)="session.retry()">Try again</button>
      </div>
    }
    <mat-sidenav-container class="container">
      <mat-sidenav #navigation [mode]="handset() ? 'over' : 'side'" [opened]="!handset()">
        <mat-nav-list>
          @for (entry of entries(); track entry.path) {
            <a
              mat-list-item
              [routerLink]="entry.path"
              routerLinkActive="active-entry"
              [routerLinkActiveOptions]="{ exact: entry.path === '/' }"
              (click)="handset() && navigation.close()"
              >{{ entry.label }}</a
            >
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content>
        <main class="content">
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100vh;
    }
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
    .loading {
      height: 4px;
      flex: none;
    }
    .banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.5rem 1rem;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .container {
      flex: 1;
    }
    mat-sidenav {
      width: 14rem;
    }
    .active-entry {
      background: var(--mat-sys-secondary-container);
    }
    .content {
      padding: 1.5rem;
    }
  `,
})
export class Shell {
  protected readonly session = inject(SessionService);
  protected readonly loading = inject(LoadingService);

  protected readonly handset = toSignal(
    inject(BreakpointObserver)
      .observe(Breakpoints.Handset)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly user = computed(() => {
    const state = this.session.state();
    return state.kind === 'signed-in' ? state.user : null;
  });

  private readonly userTypes = signal<LookupValue[]>([]);

  protected readonly role = computed(() => {
    const user = this.user();
    return user ? (this.userTypes().find((type) => type.code === user.userType)?.name ?? '') : '';
  });

  protected readonly entries = computed(() =>
    this.user()?.userType === 'platform' ? [homeEntry, tenantsEntry] : [homeEntry, usersEntry, rolesEntry],
  );

  constructor() {
    const lookups = inject(LookupsApi);
    let requested = false;
    effect(() => {
      if (this.user() && !requested) {
        requested = true;
        lookups.values(userTypeList, { background: true }).subscribe({ next: (types) => this.userTypes.set(types), error: () => this.userTypes.set([]) });
      }
    });

    const announcer = inject(LiveAnnouncer);
    effect(() => {
      if (this.loading.visible()) {
        void announcer.announce('Loading', 'polite');
      }
    });

    const dialogs = inject(MatDialog);
    inject(DestroyRef).onDestroy(() => dialogs.closeAll());
  }
}
