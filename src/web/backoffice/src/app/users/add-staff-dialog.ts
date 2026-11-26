import { Component, inject, signal } from '@angular/core';
import { FormField, ValidationError, form, maxLength, minLength, required, submit, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { problemOf } from '../problems';
import { SessionService } from '../session/session.service';
import { UsersApi } from './users-api';

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const genericFailure = 'The staff user could not be added. Please try again.';

interface NewStaffModel {
  email: string;
  password: string;
}

type FieldName = keyof NewStaffModel;

@Component({
  selector: 'app-add-staff-dialog',
  imports: [MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, FormField],
  template: `
    <h2 mat-dialog-title>Add staff user</h2>
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
          <mat-label>Email</mat-label>
          <input matInput type="email" autocomplete="off" data-field="email" [formField]="f.email" />
          @for (error of f.email().errors(); track error.kind + error.message) {
            <mat-error>{{ error.message }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>First password</mat-label>
          <input matInput type="password" autocomplete="new-password" data-field="password" [formField]="f.password" />
          <mat-hint>At least 8 characters</mat-hint>
          @for (error of f.password().errors(); track error.kind + error.message) {
            <mat-error>{{ error.message }}</mat-error>
          }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close [disabled]="f().submitting()">Cancel</button>
        <button mat-flat-button type="submit" [disabled]="f().invalid() || f().submitting()">Add</button>
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
export class AddStaffDialog {
  protected readonly session = inject(SessionService);
  private readonly api = inject(UsersApi);
  private readonly dialog = inject<MatDialogRef<AddStaffDialog, string>>(MatDialogRef);

  protected readonly model = signal<NewStaffModel>({ email: '', password: '' });
  protected readonly topError = signal<string | null>(null);

  protected readonly f = form(this.model, (path) => {
    required(path.email, { message: 'Required.' });
    validate(path.email, ({ value }) => {
      const email = value().trim();
      return email === '' || emailPattern.test(email) ? undefined : { kind: 'email', message: 'Enter a valid email address.' };
    });
    maxLength(path.email, 256, { message: 'At most 256 characters.' });
    required(path.password, { message: 'Required.' });
    minLength(path.password, 8, { message: 'At least 8 characters.' });
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
      const email = value.email.trim();
      try {
        await firstValueFrom(this.api.create({ email, password: value.password }));
        this.dialog.close(email);
        return undefined;
      } catch (error) {
        return this.serviceErrors(error);
      }
    });
  }

  private serviceErrors(error: unknown): ValidationError.WithOptionalFieldTree[] {
    const problem = problemOf(error);
    if (problem?.status === 409 && problem.code === 'email-taken') {
      return [this.fieldError('email', 'This email is already in use.')];
    }

    if (problem?.status === 400 && problem.code === 'validation-failed') {
      const errors: ValidationError.WithOptionalFieldTree[] = [];
      const unplaced: string[] = [];
      for (const [field, messages] of Object.entries(problem.errors)) {
        if (field === 'email' || field === 'password') {
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

  private fieldError(field: FieldName, message: string): ValidationError.WithOptionalFieldTree {
    return { kind: 'server', message, fieldTree: this.f[field] };
  }
}
