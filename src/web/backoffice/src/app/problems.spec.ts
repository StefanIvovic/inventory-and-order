import { HttpErrorResponse } from '@angular/common/http';
import { problemOf } from './problems';

const rfcConflict = 'https://tools.ietf.org/html/rfc9110#section-15.5.10';
const rfcBadRequest = 'https://tools.ietf.org/html/rfc9110#section-15.5.1';

describe('problemOf', () => {
  it('reads the code and field errors of a service validation problem', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        type: rfcBadRequest,
        title: 'One or more validation errors occurred.',
        status: 400,
        code: 'validation-failed',
        errors: { slug: ['Bad slug.'], adminEmail: ['Bad email.', 'Too long.'] },
      },
    });

    expect(problemOf(error)).toEqual({
      status: 400,
      code: 'validation-failed',
      errors: { slug: ['Bad slug.'], adminEmail: ['Bad email.', 'Too long.'] },
    });
  });

  it('prefers the service code over the RFC type', () => {
    const error = new HttpErrorResponse({ status: 409, error: { type: rfcConflict, title: 'Conflict', status: 409, code: 'tenant-slug-taken' } });

    expect(problemOf(error)).toEqual({ status: 409, code: 'tenant-slug-taken', errors: {} });
  });

  it('falls back to the type for gateway problems', () => {
    const error = new HttpErrorResponse({ status: 503, error: { type: 'identity-unavailable', status: 503 } });

    expect(problemOf(error)?.code).toBe('identity-unavailable');
  });

  it('ignores field errors that are not lists of strings', () => {
    const error = new HttpErrorResponse({ status: 400, error: { code: 'x', errors: { slug: 'nope', name: [1, 'Ok.'] } } });

    expect(problemOf(error)?.errors).toEqual({ name: ['Ok.'] });
  });

  it('has no code for a non-object body', () => {
    const error = new HttpErrorResponse({ status: 500, error: 'oops' });

    expect(problemOf(error)).toEqual({ status: 500, code: undefined, errors: {} });
  });

  it('is undefined for anything that is not an HTTP error', () => {
    expect(problemOf(new Error('x'))).toBeUndefined();
  });
});
