import { Component, ElementRef, booleanAttribute, computed, effect, inject, input, model, output, signal, untracked, viewChild } from '@angular/core';
import { FormValueControl, ValidationError } from '@angular/forms/signals';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent, MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { ErrorStateMatcher } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';
import { LookupValue, LookupsApi, identityService } from './lookups-api';

export type LookupSelectValue = string | null | readonly string[];

export const lookupPageSize = 50;
const namesChunk = LookupsApi.maxValues;
const scrollSlack = 48;
const unavailable = 'Unavailable';

type OptionToken = { kind: 'value'; item: LookupValue } | { kind: 'empty' } | { kind: 'retry' };

@Component({
  selector: 'app-lookup-select',
  imports: [MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatChipsModule, MatButtonModule, MatProgressBarModule],
  template: `
    <mat-form-field class="field">
      <mat-label>{{ label() }}</mat-label>
      @if (multiple()) {
        <mat-chip-grid #grid [disabled]="locked()" [errorStateMatcher]="errorMatcher" [required]="required()">
          @for (key of chosenKeys(); track key) {
            <mat-chip-row [attr.data-key]="key" (removed)="remove(key)">
              {{ nameOf(key) }}
              <button matChipRemove type="button" [attr.aria-label]="'Remove ' + nameOf(key)">×</button>
            </mat-chip-row>
          }
          <input
            #search
            data-lookup-input
            [value]="text()"
            [readonly]="readonly()"
            [matChipInputFor]="grid"
            [matChipInputSeparatorKeyCodes]="[]"
            [matAutocomplete]="auto"
            [matAutocompleteDisabled]="locked()"
            (input)="type($event)"
            (keydown.enter)="enter($event)"
            (keydown.backspace)="removeLast()"
            (blur)="touch.emit()"
          />
        </mat-chip-grid>
      } @else {
        <input
          #search
          matInput
          data-lookup-input
          [value]="text()"
          [disabled]="disabled()"
          [readonly]="readonly()"
          [required]="required()"
          [errorStateMatcher]="errorMatcher"
          [matAutocomplete]="auto"
          [matAutocompleteDisabled]="locked()"
          (input)="type($event)"
          (keydown.enter)="enter($event)"
          (blur)="touch.emit()"
        />
      }
      @if (clearable()) {
        <button matIconButton matSuffix type="button" data-lookup-clear [attr.aria-label]="'Clear ' + label()" (click)="clear()">×</button>
      }
      <button matIconButton matSuffix type="button" data-lookup-search [disabled]="locked()" [attr.aria-label]="'Search ' + label()" (click)="searchClicked()">
        <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
          <path fill="currentColor" d="M15.5 14h-.8l-.3-.3A6.5 6.5 0 1 0 14 15.5l.3.3v.8l5 5 1.5-1.5-5-5Zm-6 0A4.5 4.5 0 1 1 14 9.5 4.5 4.5 0 0 1 9.5 14Z" />
        </svg>
      </button>
      <mat-autocomplete #auto (optionSelected)="pick($event)" (opened)="opened()">
        @if (emptyOption()) {
          <mat-option [value]="emptyToken">{{ emptyLabel() }}</mat-option>
        }
        @for (item of options(); track item.id) {
          <mat-option [value]="{ kind: 'value', item }" [class.chosen]="isChosen(item)">
            {{ item.name }}
            @if (multiple() && isChosen(item)) {
              <span class="mark" aria-label="chosen">✓</span>
            }
          </mat-option>
        }
        @if (loading() || !loaded()) {
          <mat-option disabled class="state" aria-label="Loading options"><mat-progress-bar mode="indeterminate" aria-hidden="true" /></mat-option>
        } @else if (failed()) {
          <mat-option disabled class="state">Options could not be loaded.</mat-option>
          <mat-option [value]="retryToken">Try again</mat-option>
        } @else if (loaded() && options().length === 0) {
          <mat-option disabled class="state">No matches</mat-option>
        }
      </mat-autocomplete>
      @if (shownErrors().length > 0) {
        <mat-hint class="error" role="alert">
          @for (error of shownErrors(); track error.kind + error.message) {
            <span>{{ error.message }}</span>
          }
        </mat-hint>
      }
    </mat-form-field>
  `,
  styles: `
    :host {
      display: inline-block;
    }
    .field {
      width: 100%;
    }
    .mark {
      margin-inline-start: 0.5rem;
    }
    mat-progress-bar {
      width: 100%;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
})
export class LookupSelect implements FormValueControl<LookupSelectValue> {
  private readonly api = inject(LookupsApi);

  readonly typeCode = input.required<string>();
  readonly label = input.required<string>();
  readonly multiple = input(false, { transform: booleanAttribute });
  readonly valueBy = input<'code' | 'id'>('code');
  readonly emptyLabel = input<string | null>(null);
  readonly service = input(identityService);

  readonly value = model<LookupSelectValue>(null);
  readonly disabled = input(false, { transform: booleanAttribute });
  readonly readonly = input(false, { transform: booleanAttribute });
  readonly required = input(false, { transform: booleanAttribute });
  readonly touched = input(false, { transform: booleanAttribute });
  readonly errors = input<readonly ValidationError.WithOptionalFieldTree[]>([]);
  readonly touch = output<void>();

  protected readonly emptyToken: OptionToken = { kind: 'empty' };
  protected readonly retryToken: OptionToken = { kind: 'retry' };

  protected readonly text = signal('');
  protected readonly options = signal<LookupValue[]>([]);
  protected readonly loading = signal(false);
  protected readonly failed = signal(false);
  protected readonly loaded = signal(false);
  private readonly names = signal(new Map<string, string | null>());
  private readonly trigger = viewChild(MatAutocompleteTrigger);
  private readonly input = viewChild<ElementRef<HTMLInputElement>>('search');

  private query = '';
  private picking = false;
  private cursor: string | null = null;
  private lastAfter: string | null = null;
  private generation = 0;
  private readonly watchedPanels = new WeakSet<HTMLElement>();
  private readonly requested = new Set<string>();

  protected readonly locked = computed(() => this.disabled() || this.readonly());
  protected readonly chosenKeys = computed(() => {
    const value = this.value();
    return Array.isArray(value) ? (value as readonly string[]) : [];
  });
  protected readonly emptyOption = computed(() => !this.multiple() && this.emptyLabel() !== null);
  protected readonly clearable = computed(
    () => !this.multiple() && !this.required() && this.emptyLabel() === null && this.value() !== null && !this.locked(),
  );
  protected readonly shownErrors = computed(() => (this.touched() ? this.errors() : []));

  protected readonly errorMatcher: ErrorStateMatcher = { isErrorState: () => this.touched() && this.errors().length > 0 };

  constructor() {
    effect(() => {
      const keys = this.keysOf(this.value());
      untracked(() => this.fetchNames(keys));
    });

    effect(() => {
      const value = this.value();
      this.names();
      if (this.multiple()) {
        return;
      }

      untracked(() => {
        if (!this.trigger()?.panelOpen) {
          this.text.set(typeof value === 'string' ? this.nameOf(value) : '');
        }
      });
    });
  }

  protected nameOf(key: string): string {
    const names = this.names();
    if (!names.has(key)) {
      return '';
    }

    return names.get(key) ?? unavailable;
  }

  protected isChosen(item: LookupValue): boolean {
    const key = this.keyOf(item);
    const value = this.value();
    return Array.isArray(value) ? value.includes(key) : value === key;
  }

  protected type(event: Event): void {
    this.text.set((event.target as HTMLInputElement).value);
  }

  protected enter(event: Event): void {
    if (event.defaultPrevented || this.picking || this.trigger()?.activeOption) {
      return;
    }

    event.preventDefault();
    this.runSearch();
  }

  protected searchClicked(): void {
    this.runSearch();
    this.input()?.nativeElement.focus();
  }

  protected opened(): void {
    if (!this.loaded() && !this.loading()) {
      this.loadFirst();
    }

    this.watchPanel();
  }

  protected pick(event: MatAutocompleteSelectedEvent): void {
    const token = event.option.value as OptionToken;
    event.option.deselect();
    this.picking = true;
    queueMicrotask(() => (this.picking = false));
    if (token.kind === 'retry') {
      this.retry();
      queueMicrotask(() => this.trigger()?.openPanel());
      return;
    }

    if (token.kind === 'empty') {
      this.value.set(null);
      this.text.set('');
      return;
    }

    const key = this.keyOf(token.item);
    this.remember(key, token.item.name);
    if (this.multiple()) {
      const chosen = this.chosenKeys();
      if (!chosen.includes(key)) {
        this.value.set([...chosen, key]);
      }

      this.text.set('');
      if (this.input()) {
        this.input()!.nativeElement.value = '';
      }
    } else {
      this.value.set(key);
      this.text.set(token.item.name);
    }
  }

  protected remove(key: string): void {
    if (this.locked()) {
      return;
    }

    this.value.set(this.chosenKeys().filter((candidate) => candidate !== key));
  }

  protected removeLast(): void {
    const chosen = this.chosenKeys();
    if (this.text() === '' && chosen.length > 0 && !this.locked()) {
      this.value.set(chosen.slice(0, -1));
    }
  }

  protected clear(): void {
    this.value.set(null);
    this.text.set('');
  }

  private runSearch(): void {
    if (this.locked()) {
      return;
    }

    const text = this.text().trim();
    this.query = !this.multiple() && typeof this.value() === 'string' && text === this.nameOf(this.value() as string) ? '' : text;
    this.loadFirst();
    this.trigger()?.openPanel();
  }

  private loadFirst(): void {
    this.cursor = null;
    this.options.set([]);
    this.load(null);
  }

  private loadMore(): void {
    if (this.cursor && !this.loading() && !this.failed()) {
      this.load(this.cursor);
    }
  }

  private retry(): void {
    this.load(this.lastAfter);
  }

  private load(after: string | null): void {
    const generation = ++this.generation;
    this.lastAfter = after;
    this.loading.set(true);
    this.failed.set(false);
    this.api.page(this.typeCode(), { q: this.query, size: lookupPageSize, after }, this.service()).subscribe({
      next: (page) => {
        if (generation !== this.generation) {
          return;
        }

        this.options.update((options) => (after === null ? page.items : [...options, ...page.items]));
        this.cursor = page.nextCursor;
        this.loading.set(false);
        this.loaded.set(true);
        queueMicrotask(() => this.watchPanel());
      },
      error: () => {
        if (generation === this.generation) {
          this.loading.set(false);
          this.failed.set(true);
          this.loaded.set(true);
        }
      },
    });
  }

  private watchPanel(): void {
    const panel = this.trigger()?.autocomplete.panel?.nativeElement as HTMLElement | undefined;
    if (!panel || this.watchedPanels.has(panel)) {
      return;
    }

    this.watchedPanels.add(panel);
    panel.addEventListener('scroll', () => {
      if (panel.scrollTop + panel.clientHeight >= panel.scrollHeight - scrollSlack) {
        this.loadMore();
      }
    });
  }

  private fetchNames(keys: readonly string[]): void {
    const missing = keys.filter((key) => !this.names().has(key) && !this.requested.has(key));
    if (missing.length === 0) {
      return;
    }

    for (const key of missing) {
      this.requested.add(key);
    }

    const chunks: string[][] = [];
    for (let start = 0; start < missing.length; start += namesChunk) {
      chunks.push(missing.slice(start, start + namesChunk));
    }

    const byId = this.valueBy() === 'id';
    forkJoin(
      chunks.map((chunk) =>
        this.api.page(this.typeCode(), { size: chunk.length, ...(byId ? { ids: chunk } : { codes: chunk }) }, this.service()),
      ),
    ).subscribe({
      next: (pages) => {
        const found = new Map(pages.flatMap((page) => page.items).map((item) => [this.keyOf(item), item.name]));
        this.names.update((names) => {
          const next = new Map(names);
          for (const key of missing) {
            next.set(key, found.get(key) ?? null);
          }

          return next;
        });
      },
      error: () =>
        this.names.update((names) => {
          const next = new Map(names);
          for (const key of missing) {
            next.set(key, null);
          }

          return next;
        }),
    });
  }

  private remember(key: string, name: string): void {
    this.requested.add(key);
    this.names.update((names) => new Map(names).set(key, name));
  }

  private keyOf(item: LookupValue): string {
    return this.valueBy() === 'id' ? item.id : item.code;
  }

  private keysOf(value: LookupSelectValue): readonly string[] {
    if (value === null) {
      return [];
    }

    return typeof value === 'string' ? [value] : value;
  }
}
