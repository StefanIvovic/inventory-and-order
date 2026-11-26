import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { loadingInterceptor } from './loading/loading.interceptor';
import { apiInterceptor } from './session/api.interceptor';
import { SessionService } from './session/session.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([loadingInterceptor, apiInterceptor])),
    provideAppInitializer(() => {
      void inject(SessionService).load();
    }),
  ],
};
