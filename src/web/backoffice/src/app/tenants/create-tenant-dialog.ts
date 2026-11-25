import { Component, inject, signal } from '@angular/core';
import { FormField, ValidationError, form, maxLength, minLength, required, submit, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { problemOf } from '../problems';
import { SessionService } from '../session/session.service';
import { TenantsApi } from './tenants-api';

const identifierPattern = /^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$/;
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const genericFailure = 'The tenant could not be created. Please try again.';

interface NewTenantModel {
  slug: string;
  name: string;
  adminEmail: string;
  adminPassword: string;
}

type FieldName = keyof NewTenantModel;

@Component({
  selector: 'app-create-tenant-dialog',
  imports: [MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FormField],
  template: `
    <h2 mat-dialog-title>Create tenant</h2>
    <form novalidate (submit)="onSubmit($event)">
      <mat-dialog-content>
        @if (session.retryNeeded()) {
          <div class="notice" role="status">
            <span>Sign-in service is unavailable.</span>
            <button mat-button type="button" (click)="session.retry()">Try again</button>
          </div>
        }
        @if (topError(); as message) {
          <p class="error" role="alert">{{ message }}</p>
        }
        <mat-form-field>
          <mat-label>Identifier</mat-label>
          <input matInput autocomplete="off" data-field="slug" [formField]="f.slug" />
          <mat-hint>3–63 lowercase letters, digits or single hyphens</mat-hint>
          @for (error of f.slug().errors(); track error.kind + error.message) {
            <mat-error>{{ error.message }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Name</mat-label>
          <input matInput autocomplete="off" data-field="name" [formField]="f.name" />
          @for (error of f.name().errors(); track error.kind + error.message) {
            <mat-error>{{ error.message }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>First admin email</mat-label>
          <input matInput type="email" autocomplete="off" data-field="adminEmail" [formField]="f.adminEmail" />
          @for (error of f.adminEmail().errors(); track error.kind + error.message) {
            <mat-error>{{ error.message }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>First admin password</mat-label>
          <input matInput type="password" autocomplete="new-password" data-field="adminPassword" [formField]="f.adminPassword" />
          <mat-hint>At least 8 characters</mat-hint>
          @for (error of f.adminPassword().errors(); track error.kind + error.message) {
            <mat-error>{{ error.message }}</mat-error>
          }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close [disabled]="f().submitting()">Cancel</button>
        <button mat-flat-button type="submit" [disabled]="f().invalid() || f().submitting()">Create</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    mat-dialog-content {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      min-width: min(26rem, 80vw);
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
export class CreateTenantDialog {
  protected readonly session = inject(SessionService);
  private readonly api = inject(TenantsApi);
  private readonly dialog = inject<MatDialogRef<CreateTenantDialog, string>>(MatDialogRef);

  protected readonly model = signal<NewTenantModel>({ slug: '', name: '', adminEmail: '', adminPassword: '' });
  protected readonly topError = signal<string | null>(null);

  protected readonly f = form(this.model, (path) => {
    required(path.slug, { message: 'Required.' });
    validate(path.slug, ({ value }) => {
      const identifier = value().trim().toLowerCase();
      return identifier === '' || identifierPattern.test(identifier)
        ? undefined
        : { kind: 'identifier', message: 'Use 3–63 lowercase letters, digits or single hyphens.' };
    });
    required(path.name, { message: 'Required.' });
    maxLength(path.name, 200, { message: 'At most 200 characters.' });
    required(path.adminEmail, { message: 'Required.' });
    validate(path.adminEmail, ({ value }) => {
      const email = value().trim();
      return email === '' || emailPattern.test(email) ? undefined : { kind: 'email', message: 'Enter a valid email address.' };
    });
    maxLength(path.adminEmail, 256, { message: 'At most 256 characters.' });
    required(path.adminPassword, { message: 'Required.' });
    minLength(path.adminPassword, 8, { message: 'At least 8 characters.' });
  });

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void this.save();
  }

  private async save(): Promise<void> {
    if (this.f().submitting()) {
      return;
    }

    this.topError.set(null);
    await submit(this.f, async () => {
      const value = this.model();
      try {
        const created = await firstValueFrom(
          this.api.create({
            slug: value.slug.trim().toLowerCase(),
            name: value.name.trim(),
            adminEmail: value.adminEmail.trim(),
            adminPassword: value.adminPassword,
          }),
        );
        this.dialog.close(created.slug);
        return undefined;
      } catch (error) {
        return this.serviceErrors(error);
      }
    });
  }

  private serviceErrors(error: unknown): ValidationError.WithOptionalFieldTree[] {
    const problem = problemOf(error);
    if (problem?.status === 409 && problem.code === 'tenant-slug-taken') {
      return [this.fieldError('slug', 'A tenant with this identifier already exists.')];
    }

    if (problem?.status === 409 && problem.code === 'email-taken') {
      return [this.fieldError('adminEmail', 'This email is already in use.')];
    }

    if (problem?.status === 400 && problem.code === 'validation-failed') {
      const errors: ValidationError.WithOptionalFieldTree[] = [];
      const unplaced: string[] = [];
      for (const [field, messages] of Object.entries(problem.errors)) {
        if (this.isField(field)) {
          errors.push(this.fieldError(field, messages.join(' ')));
        } else {
          unplaced.push(...messages);
        }
      }

      if (unplaced.length > 0 || errors.length === 0) {
        this.topError.set(unplaced.length > 0 ? unplaced.join(' ') : genericFailure);
      }

      return errors;
    }

    this.topError.set(genericFailure);
    return [];
  }

  private isField(name: string): name is FieldName {
    return name === 'slug' || name === 'name' || name === 'adminEmail' || name === 'adminPassword';
  }

  private fieldError(field: FieldName, message: string): ValidationError.WithOptionalFieldTree {
    return { kind: 'server', message, fieldTree: this.f[field] };
  }
}
