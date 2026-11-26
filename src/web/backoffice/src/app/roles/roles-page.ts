import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { problemOf } from '../problems';
import { SessionService } from '../session/session.service';
import { Role, RolesApi } from './roles-api';

type PageState =
  | { kind: 'not-available' }
  | { kind: 'loading' }
  | { kind: 'no-access' }
  | { kind: 'error' }
  | { kind: 'loaded'; roles: Role[] };

@Component({
  selector: 'app-roles-page',
  imports: [MatButtonModule, MatProgressBarModule],
  template: `
    @switch (state().kind) {
      @case ('not-available') {
        <p>This page isn't available to you.</p>
      }
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Loading roles" />
      }
      @case ('no-access') {
        <p>You don't have access to roles.</p>
      }
      @case ('error') {
        <p>Roles could not be loaded.</p>
        <button mat-button type="button" (click)="load()">Try again</button>
      }
      @case ('loaded') {
        <h1>Roles</h1>
        @for (role of roles(); track role.name) {
          <section>
            <h2>{{ role.displayName }}</h2>
            <ul>
              @for (permission of role.permissions; track permission.name) {
                <li>{{ permission.displayName }}</li>
              }
            </ul>
          </section>
        }
      }
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
    h2 {
      font: var(--mat-sys-title-medium);
    }
  `,
})
export class RolesPage {
  private readonly api = inject(RolesApi);
  private readonly session = inject(SessionService);

  protected readonly state = signal<PageState>({ kind: 'loading' });

  protected readonly roles = () => {
    const state = this.state();
    return state.kind === 'loaded' ? state.roles : [];
  };

  constructor() {
    const state = this.session.state();
    if (state.kind === 'signed-in' && state.user.userType !== 'platform') {
      this.load();
    } else {
      this.state.set({ kind: 'not-available' });
    }
  }

  protected load(): void {
    this.state.set({ kind: 'loading' });
    this.api.list().subscribe({
      next: (list) => this.state.set({ kind: 'loaded', roles: list.roles }),
      error: (error: unknown) => this.state.set({ kind: problemOf(error)?.status === 403 ? 'no-access' : 'error' }),
    });
  }
}
