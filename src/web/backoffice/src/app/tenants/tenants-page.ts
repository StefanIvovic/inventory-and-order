import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { problemOf } from '../problems';
import { SessionService } from '../session/session.service';
import { CreateTenantDialog } from './create-tenant-dialog';
import { TenantList, TenantsApi } from './tenants-api';

const searchMaxLength = 200;

const pageSizes = [10, 25, 50];
const defaultSize = 25;

type PageState = { kind: 'not-available' } | { kind: 'loading' } | { kind: 'error' } | { kind: 'loaded' };

@Component({
  selector: 'app-tenants-page',
  imports: [MatTableModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatProgressBarModule, DatePipe],
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
        <button mat-button type="button" (click)="start()">Try again</button>
      }
      @case ('loaded') {
        <header>
          <h1>Tenants</h1>
          @if (page()?.actions?.canCreate) {
            <button mat-flat-button type="button" (click)="create()">Create tenant</button>
          }
        </header>
        <mat-form-field class="search">
          <mat-label>Search tenants</mat-label>
          <input
            matInput
            type="search"
            data-filter="q"
            [maxLength]="searchMaxLength"
            [value]="searchText()"
            (input)="typeSearch($event)"
            (keydown.enter)="applySearch()"
          />
          <button matIconButton matSuffix type="button" aria-label="Search tenants" (click)="applySearch()">
            <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
              <path fill="currentColor" d="M15.5 14h-.8l-.3-.3A6.5 6.5 0 1 0 14 15.5l.3.3v.8l5 5 1.5-1.5-5-5Zm-6 0A4.5 4.5 0 1 1 14 9.5 4.5 4.5 0 0 1 9.5 14Z" />
            </svg>
          </button>
        </mat-form-field>
        @if (items().length === 0) {
          @if (query()) {
            <p>No tenants match this search.</p>
            <button mat-button type="button" (click)="clearSearch()">Clear search</button>
          } @else {
            <p>No tenants yet.</p>
          }
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
    footer {
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
    .search {
      width: 20rem;
      max-width: 100%;
    }
  `,
})
export class TenantsPage {
  private readonly api = inject(TenantsApi);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly session = inject(SessionService);

  protected readonly columns = ['slug', 'name', 'createdAt'];
  protected readonly sizes = pageSizes;
  protected readonly searchMaxLength = searchMaxLength;
  protected readonly searchText = signal('');
  protected readonly query = signal('');
  protected readonly state = signal<PageState>({ kind: 'loading' });
  protected readonly size = signal(defaultSize);
  protected readonly index = signal(0);
  protected readonly page = signal<TenantList | null>(null);
  protected readonly total = signal<number | null>(null);
  protected readonly fetching = signal(false);

  private cursors: (string | null)[] = [null];
  private readonly cache = new Map<string, TenantList>();
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
    return total === null ? `${from}–${to}` : `${from}–${to} of ${total.toLocaleString('en-US')} tenants`;
  });

  constructor() {
    const state = this.session.state();
    if (state.kind === 'signed-in' && state.user.userType === 'platform') {
      this.start();
    } else {
      this.state.set({ kind: 'not-available' });
    }
  }

  protected start(): void {
    this.state.set({ kind: 'loading' });
    this.restart();
  }

  protected typeSearch(event: Event): void {
    this.searchText.set((event.target as HTMLInputElement).value);
  }

  protected applySearch(): void {
    this.query.set(this.searchText().trim());
    this.restart();
  }

  protected clearSearch(): void {
    this.searchText.set('');
    this.query.set('');
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

  protected create(): void {
    this.dialog
      .open<CreateTenantDialog, unknown, string>(CreateTenantDialog)
      .afterClosed()
      .subscribe((slug) => {
        if (slug) {
          this.snackBar.open(`Tenant ${slug} created.`, undefined, { duration: 5000 });
          this.restart();
        }
      });
  }

  private restart(): void {
    this.cursors = [null];
    this.index.set(0);
    this.cache.clear();
    this.total.set(null);
    this.open(null, true);
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
          this.state.set({ kind: 'loaded' });
          this.show(after, list, includeTotal);
        }
      },
      error: (error: unknown) => {
        if (generation !== this.generation) {
          return;
        }

        this.fetching.set(false);
        if (after !== null && problemOf(error)?.code === 'invalid-cursor') {
          this.restart();
        } else {
          this.state.set({ kind: 'error' });
        }
      },
    });
  }

  private show(after: string | null, list: TenantList, withTotal: boolean): void {
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
    this.api.list(this.request(after, false), { background: true }).subscribe({
      next: (list) => this.cache.set(key, list),
      error: () => undefined,
    });
  }

  private request(after: string | null, includeTotal: boolean) {
    return { q: this.query(), size: this.size(), after, includeTotal };
  }

  private key(after: string | null): string {
    return JSON.stringify({ q: this.query(), size: this.size(), after });
  }
}
