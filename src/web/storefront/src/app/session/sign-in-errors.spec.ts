import { signInErrorMessage } from './sign-in-errors';

describe('signInErrorMessage', () => {
  it.each([
    ['access_denied', "This account can't be used in this shop."],
    ['invalid_request', "This shop isn't available for sign-in."],
    ['server_error', 'Sign-in is temporarily unavailable. Please try again.'],
    ['something_else', 'Sign-in failed. Please try again.'],
  ])('maps %s', (code, message) => {
    expect(signInErrorMessage(code)).toBe(message);
  });
});
