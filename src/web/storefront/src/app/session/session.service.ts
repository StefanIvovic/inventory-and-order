import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, Subject, catchError, firstValueFrom, map, of } from 'rxjs';
import { shopSlugFromPath } from '../shop';
import { BROWSER_LOCATION } from './browser-location';

export interface SessionUser {
  userId: string;
  userType: 'customer';
  tenantId: string;
  email: string | null;
  logoutUrl: string;
  slug: string;
}

export type SessionState =
  | { kind: 'loading' }
  | { kind: 'no-shop' }
  | { kind: 'visitor'; error: string | null }
  | { kind: 'unavailable' }
  | { kind: 'signed-in'; user: SessionUser };

const signInErrorParameter = 'signin-error';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly location = inject(BROWSER_LOCATION);
  private readonly current = signal<SessionState>({ kind: 'loading' });
  private readonly retryState = signal(false);
  private readonly retries = new Subject<void>();
  private pendingSignInError: string | null | undefined;

  readonly shop = shopSlugFromPath(this.location.path());
  readonly userUrl = `/bff/shop/${this.shop}/user`;
  readonly loginUrl = `/bff/shop/${this.shop}/login`;
  readonly state = this.current.asReadonly();
  readonly retryNeeded = this.retryState.asReadonly();
  readonly retryRequested: Observable<void> = this.retries.asObservable();

  async load(): Promise<void> {
    if (this.shop === null) {
      this.current.set({ kind: 'no-shop' });
      return;
    }

    const signInError = this.takeSignInError();
    this.current.set({ kind: 'loading' });
    const next = await firstValueFrom(
      this.http.get<SessionUser>(this.userUrl).pipe(
        map((user): SessionState => ({ kind: 'signed-in', user })),
        catchError((error: unknown) =>
          of<SessionState>(
            error instanceof HttpErrorResponse && error.status === 401
              ? { kind: 'visitor', error: signInError }
              : { kind: 'unavailable' },
          ),
        ),
      ),
    );
    this.current.set(next);
  }

  signIn(): void {
    const query = new URLSearchParams(this.location.search());
    query.delete(signInErrorParameter);
    const rest = query.toString();
    const returnPath = this.location.path() + (rest ? `?${rest}` : '');
    this.location.assign(`${this.loginUrl}?returnUrl=${encodeURIComponent(returnPath)}`);
  }

  signOut(): void {
    const state = this.current();
    if (state.kind === 'signed-in') {
      this.location.assign(state.user.logoutUrl);
    }
  }

  markVisitor(): void {
    this.current.set({ kind: 'visitor', error: null });
  }

  requireRetry(): void {
    this.retryState.set(true);
  }

  retry(): void {
    this.retryState.set(false);
    this.retries.next();
  }

  private takeSignInError(): string | null {
    if (this.pendingSignInError !== undefined) {
      this.pendingSignInError = null;
      return null;
    }

    const query = new URLSearchParams(this.location.search());
    const code = query.get(signInErrorParameter);
    this.pendingSignInError = code;
    if (code !== null) {
      query.delete(signInErrorParameter);
      const rest = query.toString();
      this.location.replaceSearch(rest ? `?${rest}` : '');
    }

    return code;
  }
}
