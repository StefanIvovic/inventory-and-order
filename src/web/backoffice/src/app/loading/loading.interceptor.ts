import { HttpContext, HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { defer, finalize } from 'rxjs';
import { LoadingService } from './loading.service';

export const BACKGROUND = new HttpContextToken<boolean>(() => false);

export interface RequestOptions {
  background?: boolean;
}

export function background(): HttpContext {
  return new HttpContext().set(BACKGROUND, true);
}

export function contextFor(options: RequestOptions): HttpContext | undefined {
  return options.background ? background() : undefined;
}

export const loadingInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(BACKGROUND)) {
    return next(request);
  }

  const loading = inject(LoadingService);
  return defer(() => {
    const end = loading.begin();
    return next(request).pipe(finalize(end));
  });
};
