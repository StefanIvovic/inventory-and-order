import { Injectable, signal } from '@angular/core';

export const loadingDelay = 200;

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly shown = signal(false);
  private running = 0;
  private showTimer: ReturnType<typeof setTimeout> | null = null;
  private hideTimer: ReturnType<typeof setTimeout> | null = null;

  readonly visible = this.shown.asReadonly();

  begin(): () => void {
    this.running++;
    this.cancelHide();
    if (!this.shown() && this.showTimer === null) {
      this.showTimer = setTimeout(() => {
        this.showTimer = null;
        this.shown.set(true);
      }, loadingDelay);
    }

    let ended = false;
    return () => {
      if (ended) {
        return;
      }

      ended = true;
      this.running--;
      if (this.running === 0) {
        this.hideTimer = setTimeout(() => this.hide(), 0);
      }
    };
  }

  async track<T>(work: Promise<T>): Promise<T> {
    const end = this.begin();
    try {
      return await work;
    } finally {
      end();
    }
  }

  private hide(): void {
    this.hideTimer = null;
    if (this.showTimer !== null) {
      clearTimeout(this.showTimer);
      this.showTimer = null;
    }

    this.shown.set(false);
  }

  private cancelHide(): void {
    if (this.hideTimer !== null) {
      clearTimeout(this.hideTimer);
      this.hideTimer = null;
    }
  }
}
