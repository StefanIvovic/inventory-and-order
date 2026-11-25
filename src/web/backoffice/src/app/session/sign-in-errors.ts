const messages: Readonly<Record<string, string>> = {
  access_denied: "You don't have access to the backoffice.",
  invalid_request: 'The sign-in request was invalid. Please try again.',
  server_error: 'Sign-in is temporarily unavailable. Please try again.',
};

export function signInErrorMessage(code: string): string {
  return messages[code] ?? 'Sign-in failed. Please try again.';
}
