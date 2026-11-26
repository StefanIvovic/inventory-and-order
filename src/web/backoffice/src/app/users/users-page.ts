import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { forkJoin } from 'rxjs';
import { ConfirmData, ConfirmDialog } from '../confirm-dialog';
import { LookupValue, LookupsApi } from '../lookups/lookups-api';
import { problemOf } from '../problems';
import { Role, RolesApi } from '../roles/roles-api';
import { SessionService } from '../session/session.service';
import { AddStaffDialog } from './add-staff-dialog';
import { RolesDialog, RolesDialogData } from './roles-dialog';
import { StaffFilters, StaffItem, StaffList, UsersApi } from './users-api';

export const searchDelay = 300;
export const pageSizes = [10, 25, 50];
const defaultSize = 25;
const statusList = 'staff-status';
const activeStatus = 'active';
const disabledStatus = 'disabled';
const noFilters: StaffFilters = { q: '', status: null, role: null };

type PageState = { kind: 'not-available' } | { kind: 'loading' } | { kind: 'no-access' } | { kind: 'error' } | { kind: 'loaded' };

@Component({
  selector: 'app-users-page',
  imports: [
    MatTableModule,
    MatButtonModule,
    MatProgressBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    DatePipe,
  ],
  template: `
    @switch (state().kind) {
      @case ('not-available') {
        <p>This page isn't available to you.</p>
      }
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Loading users" />
      }
      @case ('no-access') {
        <p>You don't have access to users.</p>
      }
      @case ('error') {
        <p>Users could not be loaded.</p>
        <button mat-button type="button" (click)="start()">Try again</button>
      }
      @case ('loaded') {
        <header>
          <h1>Users</h1>
          @if (page()?.actions?.canCreate) {
            <button mat-flat-button type="button" (click)="add()">Add staff user</button>
          }
        </header>
        <div class="filters">
          <mat-form-field>
            <mat-label>Search email</mat-label>
            <input matInput type="search" data-filter="q" [value]="searchText()" (input)="search($event)" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Status</mat-label>
            <mat-select data-filter="status" [value]="filters().status" (selectionChange)="filter({ status: $event.value })">
              <mat-option [value]="null">All</mat-option>
              @for (status of statuses(); track status.code) {
                <mat-option [value]="status.code">{{ status.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Role</mat-label>
            <mat-select data-filter="role" [value]="filters().role" (selectionChange)="filter({ role: $event.value })">
              <mat-option [value]="null">Any</mat-option>
              @for (role of roles(); track role.name) {
                <mat-option [value]="role.name">{{ role.displayName }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>
        @if (fetching()) {
          <mat-progress-bar mode="indeterminate" aria-label="Loading users" />
        }
        @if (items().length === 0) {
          <p>No users match these filters.</p>
          <button mat-button type="button" (click)="clearFilters()">Clear filters</button>
        } @else {
          <table mat-table [dataSource]="items()">
            <ng-container matColumnDef="email">
              <th mat-header-cell *matHeaderCellDef>Email</th>
              <td mat-cell *matCellDef="let user">{{ user.email }}</td>
            </ng-container>
            <ng-container matColumnDef="roles">
              <th mat-header-cell *matHeaderCellDef>Roles</th>
              <td mat-cell *matCellDef="let user">{{ roleNames(user) }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let user">{{ statusName(user) }}</td>
            </ng-container>
            <ng-container matColumnDef="createdAt">
              <th mat-header-cell *matHeaderCellDef>Created</th>
              <td mat-cell *matCellDef="let user">{{ user.createdAt | date: 'medium' }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef>Actions</th>
              <td mat-cell *matCellDef="let user">
                @if (user.actions.canAssignRoles) {
                  <button mat-button type="button" (click)="manageRoles(user)">Roles</button>
                }
                @if (user.actions.canDisable) {
                  <button mat-button type="button" (click)="disable(user)">Disable</button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        }
        <footer>
          <mat-form-field class="size">
            <mat-label>Rows per page</mat-label>
            <mat-select data-filter="size" [value]="size()" (selectionChange)="resize($event.value)">
              @for (option of sizes; track option) {
                <mat-option [value]="option">{{ option }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <span class="range">{{ range() }}</span>
          <button mat-button type="button" [disabled]="index() === 0 || fetching()" (click)="previous()">Previous</button>
          <button mat-button type="button" [disabled]="!page()?.nextCursor || fetching()" (click)="next()">Next</button>
        </footer>
      }
    }
  `,
  styles: `
    header,
    footer,
    .filters {
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
    }
    header {
      justify-content: space-between;
    }
    footer {
      justify-content: flex-end;
    }
    h1 {
      font: var(--mat-sys-headline-small);
    }
    table {
      width: 100%;
    }
    .size {
      width: 9rem;
    }
  `,
})
export class UsersPage {
  private readonly api = inject(UsersApi);
  private readonly rolesApi = inject(RolesApi);
  private readonly lookups = inject(LookupsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly session = inject(SessionService);

  protected readonly columns = ['email', 'roles', 'status', 'createdAt', 'actions'];
  protected readonly sizes = pageSizes;
  protected readonly state = signal<PageState>({ kind: 'loading' });
  protected readonly statuses = signal<LookupValue[]>([]);
  protected readonly roles = signal<Role[]>([]);
  protected readonly filters = signal<StaffFilters>(noFilters);
  protected readonly searchText = signal('');
  protected readonly size = signal(defaultSize);
  protected readonly index = signal(0);
  protected readonly page = signal<StaffList | null>(null);
  protected readonly total = signal<number | null>(null);
  protected readonly fetching = signal(false);

  private cursors: (string | null)[] = [null];
  private readonly cache = new Map<string, StaffList>();
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  private generation = 0;

  protected readonly items = computed(() => this.page()?.items ?? []);

  protected readonly range = computed(() => {
    const count = this.items().length;
    if (count === 0) {
      return '';
    }

    const from = this.index() * this.size() + 1;
    const to = from + count - 1;
    const total = this.total();
    return total === null ? `${from}–${to}` : `${from}–${to} of ${total.toLocaleString('en-US')} users`;
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.searchTimer));
    const state = this.session.state();
    if (state.kind === 'signed-in' && state.user.userType !== 'platform') {
      this.start();
    } else {
      this.state.set({ kind: 'not-available' });
    }
  }

  protected start(): void {
    this.state.set({ kind: 'loading' });
    this.resetPaging();
    const generation = ++this.generation;
    forkJoin({
      list: this.api.list(this.request(null, true)),
      roles: this.rolesApi.list(),
      statuses: this.lookups.values(statusList),
    }).subscribe({
      next: ({ list, roles, statuses }) => {
        if (generation !== this.generation) {
          return;
        }

        this.roles.set(roles.roles);
        this.statuses.set(statuses);
        this.state.set({ kind: 'loaded' });
        this.show(null, list, true);
      },
      error: (error: unknown) => {
        if (generation === this.generation) {
          this.state.set({ kind: problemOf(error)?.status === 403 ? 'no-access' : 'error' });
        }
      },
    });
  }

  protected roleNames(user: StaffItem): string {
    const names = user.roles
      .map((name) => this.roles().find((role) => role.name === name)?.displayName)
      .filter((name): name is string => name !== undefined);
    return names.length === 0 ? 'No roles' : names.join(', ');
  }

  protected statusName(user: StaffItem): string {
    const code = user.disabled ? disabledStatus : activeStatus;
    return this.statuses().find((status) => status.code === code)?.name ?? '';
  }

  protected search(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.searchText.set(text);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.filter({ q: text }), searchDelay);
  }

  protected filter(change: Partial<StaffFilters>): void {
    this.filters.update((filters) => ({ ...filters, ...change }));
    this.restart();
  }

  protected clearFilters(): void {
    clearTimeout(this.searchTimer);
    this.searchText.set('');
    this.filters.set(noFilters);
    this.restart();
  }

  protected resize(size: number): void {
    this.size.set(size);
    this.restart();
  }

  protected next(): void {
    const after = this.page()?.nextCursor;
    if (!after) {
      return;
    }

    this.cursors = [...this.cursors.slice(0, this.index() + 1), after];
    this.index.update((index) => index + 1);
    this.open(after, false);
  }

  protected previous(): void {
    if (this.index() === 0) {
      return;
    }

    this.index.update((index) => index - 1);
    this.open(this.cursors[this.index()] ?? null, false);
  }

  protected add(): void {
    this.dialog
      .open<AddStaffDialog, unknown, string>(AddStaffDialog)
      .afterClosed()
      .subscribe((email) => {
        if (email) {
          this.snackBar.open(`Staff user ${email} added.`, undefined, { duration: 5000 });
          this.restart();
        }
      });
  }

  protected manageRoles(user: StaffItem): void {
    this.dialog
      .open<RolesDialog, RolesDialogData, boolean>(RolesDialog, { data: { user, roles: this.roles() } })
      .afterClosed()
      .subscribe((changed) => {
        if (changed) {
          this.restart();
        }
      });
  }

  protected disable(user: StaffItem): void {
    this.dialog
      .open<ConfirmDialog, ConfirmData, boolean>(ConfirmDialog, {
        data: {
          title: 'Disable user',
          text: `Disable ${user.email}? They will lose access to the backoffice.`,
          confirm: 'Disable',
        },
      })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }

        this.api.disable(user.id).subscribe({
          next: () => {
            this.snackBar.open(`${user.email} disabled.`, undefined, { duration: 5000 });
            this.restart();
          },
          error: (error: unknown) => {
            const message =
              problemOf(error)?.code === 'cannot-disable-self' ? "You can't disable your own account." : 'The user could not be disabled.';
            this.snackBar.open(message, undefined, { duration: 5000 });
          },
        });
      });
  }

  private restart(): void {
    this.resetPaging();
    this.open(null, true);
  }

  private resetPaging(): void {
    this.cursors = [null];
    this.index.set(0);
    this.cache.clear();
    this.total.set(null);
  }

  private open(after: string | null, includeTotal: boolean): void {
    const cached = this.cache.get(this.key(after));
    if (cached && !includeTotal) {
      this.show(after, cached, false);
      return;
    }

    const generation = ++this.generation;
    this.fetching.set(true);
    this.api.list(this.request(after, includeTotal)).subscribe({
      next: (list) => {
        if (generation === this.generation) {
          this.fetching.set(false);
          this.show(after, list, includeTotal);
        }
      },
      error: (error: unknown) => {
        if (generation !== this.generation) {
          return;
        }

        this.fetching.set(false);
        const problem = problemOf(error);
        if (problem?.code === 'invalid-cursor') {
          this.restart();
        } else {
          this.state.set({ kind: problem?.status === 403 ? 'no-access' : 'error' });
        }
      },
    });
  }

  private show(after: string | null, list: StaffList, withTotal: boolean): void {
    this.cache.set(this.key(after), list);
    this.page.set(list);
    if (withTotal) {
      this.total.set(list.total ?? null);
    }

    this.prefetch(list.nextCursor);
  }

  private prefetch(after: string | null): void {
    if (!after || this.cache.has(this.key(after))) {
      return;
    }

    const key = this.key(after);
    this.api.list(this.request(after, false)).subscribe({
      next: (list) => this.cache.set(key, list),
      error: () => undefined,
    });
  }

  private request(after: string | null, includeTotal: boolean) {
    return { ...this.filters(), size: this.size(), after, includeTotal };
  }

  private key(after: string | null): string {
    return JSON.stringify({ ...this.filters(), size: this.size(), after });
  }
}
