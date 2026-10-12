import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { NotifyService } from './notify.service';
import { Problem } from './models';

/** Attaches the bearer token to API calls. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).token();
  return next(token && req.url.startsWith('/api') ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
};

/** Turns RFC 7807 problem details into a friendly snackbar; an expired session sends you to login. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const notify = inject(NotifyService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes('/auth/')) {
        auth.logout();
        notify.error('Your session has expired. Please sign in again.');
      } else {
        notify.error(describeError(error));
      }
      return throwError(() => error);
    })
  );
};

export function describeError(error: HttpErrorResponse): string {
  if (error.status === 0) return 'Cannot reach the server. Is the API running?';
  const problem = error.error as Problem | null;
  const firstFieldError = problem?.errors ? Object.values(problem.errors)[0]?.[0] : undefined;
  return firstFieldError ?? problem?.detail ?? problem?.title ?? `Request failed (${error.status}).`;
}

export const authGuard: CanActivateFn = (_, state) =>
  inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

export const guestGuard: CanActivateFn = () =>
  !inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/']);

export const managerGuard: CanActivateFn = () =>
  inject(AuthService).isManager() || inject(Router).createUrlTree(['/']);
