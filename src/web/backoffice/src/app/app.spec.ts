import { LookupsApi } from './lookups/lookups-api';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { App } from './app';
import { SessionHarness, loadAs, setUpSession, staffUser } from './session/testing/session-harness';

describe('App', () => {
  let harness: SessionHarness;

  beforeEach(() => {
    harness = setUpSession();
  });

  afterEach(() => harness.http.verify());

  function flushUserTypes(): void {
    harness.http
      .expectOne((request) => request.url === LookupsApi.valuesUrl('user-type'))
      .flush({ items: [{ id: 's', code: 'staff', name: 'Staff' }], nextCursor: null });
  }

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows only the progress bar while loading', async () => {
    const loaded = harness.session.load();
    const element = render();

    expect(element.querySelector('mat-progress-bar')).not.toBeNull();
    expect(element.textContent).not.toContain('Sign in');
    expect(element.querySelector('mat-toolbar')).toBeNull();
    harness.http.expectOne('/bff/backoffice/user').flush(staffUser);
    await loaded;
  });

  it('shows the sign-in screen without a session', async () => {
    const fixture = TestBed.createComponent(App);
    await loadAs(harness, null);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('button')?.textContent).toContain('Sign in');
    expect(element.querySelector('mat-toolbar')).toBeNull();
  });

  it('shows the frame with a session', async () => {
    const fixture = TestBed.createComponent(App);
    await loadAs(harness, staffUser);
    fixture.detectChanges();
    flushUserTypes();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('mat-toolbar')?.textContent).toContain('admin@demo.local');
    expect(element.querySelector('mat-toolbar')?.textContent).toContain('Staff');
    expect(element.textContent).not.toContain('Sign in to continue');
  });

  it('shows the retry screen when the backoffice is unavailable', async () => {
    const fixture = TestBed.createComponent(App);
    await loadAs(harness, null, 502);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('The backoffice is unavailable');
    const retry = Array.from(element.querySelectorAll('button')).find((button) => button.textContent?.includes('Try again'));
    retry?.click();
    harness.http.expectOne('/bff/backoffice/user').flush(staffUser);
  });

  it('closes open dialogs when the session ends', async () => {
    const fixture = TestBed.createComponent(App);
    await loadAs(harness, staffUser);
    fixture.detectChanges();
    flushUserTypes();
    const dialogs = TestBed.inject(MatDialog);
    const closeAll = vi.spyOn(dialogs, 'closeAll');
    const opened = dialogs.open(OpenDialog);
    fixture.detectChanges();

    harness.session.markSignedOut();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(closeAll).toHaveBeenCalled();
    expect(['closing', 'closed']).toContain(['open', 'closing', 'closed'][opened.getState()]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Sign in');
  });
});

@Component({ template: '<p>open</p>' })
class OpenDialog {}
