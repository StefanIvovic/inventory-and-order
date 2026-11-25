import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { SessionService } from '../session/session.service';
import { CreateTenantDialog } from './create-tenant-dialog';
import { TenantItem, TenantsApi } from './tenants-api';

type PageState =
  | { kind: 'not-available' }
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'loaded'; items: TenantItem[]; canCreate: boolean };

@Component({
  selector: 'app-tenants-page',
  imports: [MatTableModule, MatButtonModule, MatProgressBarModule, DatePipe],
  template: `
    @switch (state().kind) {
      @case ('not-available') {
        <p>This page isn't available to you.</p>
      }
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Loading tenants" />
      }
      @case ('error') {
        <p>Tenants could not be loaded.</p>
        <button mat-button type="button" (click)="load()">Try again</button>
      }
      @case ('loaded') {
        <header>
          <h1>Tenants</h1>
          @if (canCreate()) {
            <button mat-flat-button type="button" (click)="create()">Create tenant</button>
          }
        </header>
        @if (items().length === 0) {
          <p>No tenants yet.</p>
        } @else {
          <table mat-table [dataSource]="items()">
            <ng-container matColumnDef="slug">
              <th mat-header-cell *matHeaderCellDef>Identifier</th>
              <td mat-cell *matCellDef="let tenant">{{ tenant.slug }}</td>
            </ng-container>
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Name</th>
              <td mat-cell *matCellDef="let tenant">{{ tenant.name }}</td>
            </ng-container>
            <ng-container matColumnDef="createdAt">
              <th mat-header-cell *matHeaderCellDef>Created</th>
              <td mat-cell *matCellDef="let tenant">{{ tenant.createdAt | date: 'medium' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        }
      }
    }
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }
    h1 {
      font: var(--mat-sys-headline-small);
    }
    table {
      width: 100%;
    }
  `,
})
export class TenantsPage {
  private readonly api = inject(TenantsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly session = inject(SessionService);

  protected readonly columns = ['slug', 'name', 'createdAt'];
  protected readonly state = signal<PageState>({ kind: 'loading' });

  protected readonly items = computed(() => {
    const state = this.state();
    return state.kind === 'loaded' ? state.items : [];
  });

  protected readonly canCreate = computed(() => {
    const state = this.state();
    return state.kind === 'loaded' && state.canCreate;
  });

  constructor() {
    const state = this.session.state();
    if (state.kind === 'signed-in' && state.user.userType === 'platform') {
      this.load();
    } else {
      this.state.set({ kind: 'not-available' });
    }
  }

  protected load(): void {
    this.state.set({ kind: 'loading' });
    this.api.list().subscribe({
      next: (list) =>
        this.state.set({
          kind: 'loaded',
          items: [...list.items].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt)),
          canCreate: list.actions.canCreate,
        }),
      error: () => this.state.set({ kind: 'error' }),
    });
  }

  protected create(): void {
    this.dialog
      .open<CreateTenantDialog, unknown, string>(CreateTenantDialog)
      .afterClosed()
      .subscribe((slug) => {
        if (slug) {
          this.snackBar.open(`Tenant ${slug} created.`, undefined, { duration: 5000 });
          this.load();
        }
      });
  }
}
