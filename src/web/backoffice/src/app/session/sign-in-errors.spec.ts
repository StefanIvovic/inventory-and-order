import { signInErrorMessage } from './sign-in-errors';

describe('signInErrorMessage', () => {
  it.each([
    ['access_denied', "You don't have access to the backoffice."],
    ['invalid_request', 'The sign-in request was invalid. Please try again.'],
    ['server_error', 'Sign-in is temporarily unavailable. Please try again.'],
    ['something_else', 'Sign-in failed. Please try again.'],
  ])('maps %s', (code, message) => {
    expect(signInErrorMessage(code)).toBe(message);
  });
});
