import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { SessionService } from '../session/session.service';

@Component({
  selector: 'app-unavailable',
  imports: [MatCardModule, MatButtonModule],
  template: `
    <main class="center">
      <mat-card appearance="outlined">
        <mat-card-content>
          <p>The backoffice is unavailable. Try again in a moment.</p>
        </mat-card-content>
        <mat-card-actions>
          <button mat-flat-button type="button" (click)="tryAgain()">Try again</button>
        </mat-card-actions>
      </mat-card>
    </main>
  `,
  styles: `
    .center {
      display: grid;
      place-items: center;
      min-height: 100vh;
    }
    mat-card {
      width: min(24rem, 90vw);
    }
  `,
})
export class Unavailable {
  private readonly session = inject(SessionService);

  protected tryAgain(): void {
    void this.session.load();
  }
}
