import { HttpErrorResponse } from '@angular/common/http';
import { addDays, apiError, toIsoDate } from './utils';

describe('utils', () => {
  describe('apiError', () => {
    it('returns the API message when one is present', () => {
      const err = new HttpErrorResponse({ status: 409, error: { status: 409, message: 'This time slot was just booked.' } });
      expect(apiError(err)).toBe('This time slot was just booked.');
    });

    it('explains when the server is unreachable', () => {
      const err = new HttpErrorResponse({ status: 0 });
      expect(apiError(err)).toContain("Can't reach the server");
    });

    it('falls back when the body has no message', () => {
      const err = new HttpErrorResponse({ status: 500, error: 'oops' });
      expect(apiError(err, 'Fallback text')).toBe('Fallback text');
    });

    it('falls back for non-HTTP errors', () => {
      expect(apiError(new Error('boom'))).toBe('Something went wrong. Try again.');
    });
  });

  describe('toIsoDate', () => {
    it('formats in local time with zero padding', () => {
      expect(toIsoDate(new Date(2026, 0, 5, 23, 59))).toBe('2026-01-05');
    });
  });

  describe('addDays', () => {
    it('crosses month and year boundaries', () => {
      expect(toIsoDate(addDays(new Date(2026, 11, 31), 1))).toBe('2027-01-01');
    });

    it('supports negative offsets', () => {
      expect(toIsoDate(addDays(new Date(2026, 2, 1), -1))).toBe('2026-02-28');
    });

    it('does not change the original date', () => {
      const original = new Date(2026, 9, 19);
      addDays(original, 5);
      expect(toIsoDate(original)).toBe('2026-10-19');
    });
  });
});
