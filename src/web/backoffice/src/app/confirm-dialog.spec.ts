import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ConfirmDialog } from './confirm-dialog';

describe('ConfirmDialog', () => {
  let closed: unknown[];

  beforeEach(() => {
    closed = [];
    TestBed.configureTestingModule({
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: { title: 'Disable user', text: 'Disable a@b.test?', confirm: 'Disable' } },
        { provide: MatDialogRef, useValue: { close: (value: unknown) => closed.push(value) } },
      ],
    });
  });

  function render(): HTMLElement {
    const fixture = TestBed.createComponent(ConfirmDialog);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const button = (element: HTMLElement, text: string) =>
    Array.from(element.querySelectorAll('button')).find((candidate) => candidate.textContent?.trim() === text)!;

  it('shows the title, the text and the confirm label', () => {
    const element = render();

    expect(element.textContent).toContain('Disable user');
    expect(element.textContent).toContain('Disable a@b.test?');
    expect(button(element, 'Disable')).toBeDefined();
  });

  it.each([
    ['Disable', true],
    ['Cancel', false],
  ])('%s closes with %s', (text, result) => {
    const element = render();

    button(element, text).click();

    expect(closed).toEqual([result]);
  });
});
