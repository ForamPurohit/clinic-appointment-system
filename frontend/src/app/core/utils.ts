import { HttpErrorResponse } from '@angular/common/http';

/** Pulls the API's { message } out of an error, with a readable fallback. */
export function apiError(err: unknown, fallback = 'Something went wrong. Try again.'): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return "Can't reach the server. Check that the API is running on the configured URL.";
    }
    const message = err.error?.message;
    if (typeof message === 'string' && message.length > 0) {
      return message;
    }
  }
  return fallback;
}

/** yyyy-MM-dd in the browser's local time zone. */
export function toIsoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function addDays(date: Date, days: number): Date {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}
