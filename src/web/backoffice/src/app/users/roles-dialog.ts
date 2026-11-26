import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSlideToggleChange, MatSlideToggleModule } from '@angular/material/slide-toggle';
import { problemOf } from '../problems';
import { Role } from '../roles/roles-api';
import { SessionService } from '../session/session.service';
import { StaffItem, UsersApi } from './users-api';

export interface RolesDialogData {
  user: StaffItem;
  roles: Role[];
}

const refusals: Readonly<Record<string, string>> = {
  'cannot-remove-own-admin': "You can't remove your own Administrator role.",
  'user-disabled': 'This user is disabled.',
};

@Component({
  selector: 'app-roles-dialog',
  imports: [MatDialogModule, MatButtonModule, MatSlideToggleModule],
  template: `
    <h2 mat-dialog-title>Roles for {{ data.user.email }}</h2>
    <mat-dialog-content>
      @if (session.retryNeeded()) {
        <div class="notice" role="status">
          <span>Sign-in service is unavailable.</span>
          <button mat-button type="button" (click)="session.retry()">Try again</button>
        </div>
      }
      @if (error(); as message) {
        <p class="error" role="alert">{{ message }}</p>
      }
      @for (role of data.roles; track role.name) {
        <mat-slide-toggle
          [attr.data-role]="role.name"
          [checked]="assigned().has(role.name)"
          [disabled]="saving().has(role.name)"
          (change)="toggle(role, $event)"
          >{{ role.displayName }}</mat-slide-toggle
        >
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="close()">Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-dialog-content {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      min-width: min(22rem, 80vw);
    }
    .error {
      color: var(--mat-sys-error);
    }
    .notice {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
      padding: 0.25rem 0.75rem;
    }
  `,
})
export class RolesDialog {
  protected readonly session = inject(SessionService);
  protected readonly data = inject<RolesDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(UsersApi);
  private readonly dialog = inject<MatDialogRef<RolesDialog, boolean>>(MatDialogRef);

  protected readonly assigned = signal(new Set(this.data.user.roles));
  protected readonly saving = signal(new Set<string>());
  protected readonly error = signal<string | null>(null);
  private changed = false;

  protected toggle(role: Role, event: MatSlideToggleChange): void {
    const on = event.checked;
    this.error.set(null);
    this.saving.update((names) => new Set(names).add(role.name));
    const request = on ? this.api.assign(this.data.user.id, role.name) : this.api.remove(this.data.user.id, role.name);
    request.subscribe({
      next: () => {
        this.changed = true;
        this.assigned.update((names) => {
          const next = new Set(names);
          if (on) {
            next.add(role.name);
          } else {
            next.delete(role.name);
          }

          return next;
        });
        this.done(role.name);
      },
      error: (error: unknown) => {
        const code = problemOf(error)?.code;
        this.error.set((code && refusals[code]) ?? 'The role could not be changed.');
        event.source.checked = !on;
        this.done(role.name);
      },
    });
  }

  protected close(): void {
    this.dialog.close(this.changed);
  }

  private done(name: string): void {
    this.saving.update((names) => {
      const next = new Set(names);
      next.delete(name);
      return next;
    });
  }
}
