const messages: Readonly<Record<string, string>> = {
  access_denied: "This account can't be used in this shop.",
  invalid_request: "This shop isn't available for sign-in.",
  server_error: 'Sign-in is temporarily unavailable. Please try again.',
};

export function signInErrorMessage(code: string): string {
  return messages[code] ?? 'Sign-in failed. Please try again.';
}
