import { HttpErrorResponse } from '@angular/common/http';

export interface Problem {
  status: number;
  code: string | undefined;
  errors: Record<string, string[]>;
}

export function problemOf(error: unknown): Problem | undefined {
  if (!(error instanceof HttpErrorResponse)) {
    return undefined;
  }

  const body: unknown = error.error;
  const record = typeof body === 'object' && body !== null ? (body as Record<string, unknown>) : {};
  const code = typeof record['code'] === 'string' ? record['code'] : typeof record['type'] === 'string' ? record['type'] : undefined;
  return { status: error.status, code, errors: fieldErrors(record['errors']) };
}

function fieldErrors(value: unknown): Record<string, string[]> {
  if (typeof value !== 'object' || value === null) {
    return {};
  }

  const result: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      const texts = messages.filter((message): message is string => typeof message === 'string');
      if (texts.length > 0) {
        result[field] = texts;
      }
    }
  }

  return result;
}
