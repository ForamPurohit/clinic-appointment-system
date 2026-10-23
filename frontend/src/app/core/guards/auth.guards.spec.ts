import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Role } from '../models/models';
import { storeSession } from '../../../testing/test-data';
import { guestGuard, roleGuard } from './auth.guards';

describe('route guards', () => {
  let router: Router;

  function setup() {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
    router = TestBed.inject(Router);
  }

  function runRoleGuard(roles: Role[] | undefined, url = '/admin/doctors') {
    const route = { data: roles ? { roles } : {} } as unknown as ActivatedRouteSnapshot;
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => roleGuard(route, state));
  }

  function runGuestGuard() {
    return TestBed.runInInjectionContext(() =>
      guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
  }

  const asUrl = (result: unknown) => router.serializeUrl(result as UrlTree);

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  describe('roleGuard', () => {
    it('sends signed-out users to sign in, remembering where they were going', () => {
      setup();
      expect(asUrl(runRoleGuard(['Admin'], '/admin/doctors'))).toBe('/login?returnUrl=%2Fadmin%2Fdoctors');
    });

    it('lets in a user with an allowed role', () => {
      storeSession('Admin');
      setup();
      expect(runRoleGuard(['Admin'])).toBeTrue();
    });

    it('sends a user with the wrong role to their own home page', () => {
      storeSession('Patient');
      setup();
      expect(asUrl(runRoleGuard(['Admin']))).toBe('/patient/appointments');
    });

    it('allows any signed-in user when the route lists no roles', () => {
      storeSession('Doctor');
      setup();
      expect(runRoleGuard(undefined)).toBeTrue();
    });
  });

  describe('guestGuard', () => {
    it('lets signed-out visitors see sign-in and register', () => {
      setup();
      expect(runGuestGuard()).toBeTrue();
    });

    it('redirects signed-in users to their workspace', () => {
      storeSession('Doctor');
      setup();
      expect(asUrl(runGuestGuard())).toBe('/doctor/schedule');
    });
  });
});
