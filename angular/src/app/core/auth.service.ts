import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storageKey = 'citatboken.session';
  readonly currentSession = signal<AuthResponse | null>(this.readSession());
  isLoggedIn(): boolean {
    const session = this.currentSession();
    return session !== null && Date.parse(session.expiresAt) > Date.now();
  }

  register(request: RegisterRequest) {
    return this.http
      .post<AuthResponse>('/api/auth/register', request)
      .pipe(tap((session) => this.saveSession(session)));
  }

  login(request: LoginRequest) {
    return this.http
      .post<AuthResponse>('/api/auth/login', request)
      .pipe(tap((session) => this.saveSession(session)));
  }

  logout(): void {
    localStorage.removeItem(this.storageKey);
    this.currentSession.set(null);
  }

  private saveSession(session: AuthResponse): void {
    localStorage.setItem(this.storageKey, JSON.stringify(session));
    this.currentSession.set(session);
  }

  private readSession(): AuthResponse | null {
    try {
      const saved = localStorage.getItem(this.storageKey);
      if (!saved) return null;

      const session = JSON.parse(saved) as AuthResponse;
      if (
        typeof session.accessToken !== 'string' ||
        typeof session.expiresAt !== 'string' ||
        Date.parse(session.expiresAt) <= Date.now()
      ) {
        localStorage.removeItem(this.storageKey);
        return null;
      }

      return session;
    } catch {
      localStorage.removeItem(this.storageKey);
      return null;
    }
  }
}
