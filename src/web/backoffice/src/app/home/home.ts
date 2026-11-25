import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionService } from '../session/session.service';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  template: `
    <h1>Welcome to the backoffice.</h1>
    @if (platform()) {
      <p><a routerLink="/tenants">Manage tenants</a></p>
    }
  `,
  styles: `
    h1 {
      font: var(--mat-sys-headline-small);
    }
  `,
})
export class Home {
  private readonly session = inject(SessionService);

  protected readonly platform = computed(() => {
    const state = this.session.state();
    return state.kind === 'signed-in' && state.user.userType === 'platform';
  });
}
