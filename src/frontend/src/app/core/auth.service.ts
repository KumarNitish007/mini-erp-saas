import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { AuthResponse, CurrentUser, RegisterRequest } from './models';

const STORAGE_KEY = 'minierp.session';

interface Session {
  token: string;
  expiresAtUtc: string;
  user: CurrentUser;
}

/** Holds the signed-in session as signals; persisted so a refresh keeps you logged in. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  private readonly session = signal<Session | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isManager = computed(() => ['Owner', 'Admin'].includes(this.user()?.role ?? ''));

  login(email: string, password: string): Observable<AuthResponse> {
    return this.api.login(email, password).pipe(tap(r => this.store(r)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.api.register(request).pipe(tap(r => this.store(r)));
  }

  logout(redirect = true): void {
    this.session.set(null);
    try { localStorage.removeItem(STORAGE_KEY); } catch { /* storage unavailable */ }
    if (redirect) this.router.navigate(['/login']);
  }

  private store(r: AuthResponse): void {
    const session: Session = { token: r.accessToken, expiresAtUtc: r.expiresAtUtc, user: r.user };
    this.session.set(session);
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(session)); } catch { /* storage unavailable */ }
  }

  private restore(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const session = JSON.parse(raw) as Session;
      return new Date(session.expiresAtUtc).getTime() > Date.now() ? session : null;
    } catch {
      return null;
    }
  }
}
