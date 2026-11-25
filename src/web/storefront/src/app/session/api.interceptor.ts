import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, switchMap, take, throwError } from 'rxjs';
import { SessionService } from './session.service';

export const appHeader = 'X-Bff-App';
export const appName = 'storefront';

export const apiInterceptor: HttpInterceptorFn = (request: HttpRequest<unknown>, next: HttpHandlerFn) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const session = inject(SessionService);
  const outgoing = request.clone({ setHeaders: { [appHeader]: appName } });

  const send = (): Observable<HttpEvent<unknown>> =>
    next(outgoing).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 401) {
          session.markVisitor();
        } else if (error instanceof HttpErrorResponse && error.status === 503 && problemType(error) === 'identity-unavailable') {
          session.requireRetry();
          return session.retryRequested.pipe(take(1), switchMap(send));
        }

        return throwError(() => error);
      }),
    );

  return send();
};

function problemType(error: HttpErrorResponse): string | undefined {
  const body: unknown = error.error;
  return typeof body === 'object' && body !== null && 'type' in body && typeof body.type === 'string' ? body.type : undefined;
}
