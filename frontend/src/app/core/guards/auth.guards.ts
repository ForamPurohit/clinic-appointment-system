import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from '../models/models';
import { AuthService } from '../services/auth.service';

/** Requires sign-in, and optionally one of the roles listed in route data: { roles: ['Admin'] } */
export const roleGuard: CanActivateFn = (route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = auth.user();

  if (!user) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  const roles = route.data['roles'] as Role[] | undefined;
  if (roles && !roles.includes(user.role)) {
    return router.createUrlTree([auth.homeRoute(user.role)]);
  }

  return true;
};

/** Keeps signed-in users away from the login and register pages. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? router.createUrlTree([auth.homeRoute()]) : true;
};
