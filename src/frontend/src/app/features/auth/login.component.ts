import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { AuthHeroComponent } from './auth-hero.component';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, AuthHeroComponent],
  template: `
    <div class="auth-page">
      <app-auth-hero />
      <section class="auth-panel">
        <div class="auth-card">
          <h1>Welcome back</h1>
          <p class="muted">Sign in to your company workspace.</p>

          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field>
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="username">
            </mat-form-field>
            <mat-form-field>
              <mat-label>Password</mat-label>
              <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password">
              <button mat-icon-button matSuffix type="button" (click)="showPassword.set(!showPassword())" aria-label="Show password">
                <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
            </mat-form-field>
            <button mat-flat-button class="submit" [disabled]="form.invalid || busy()">
              @if (busy()) { <mat-spinner diameter="20" /> } @else { Sign in }
            </button>
          </form>

          <div class="demo-hint">
            <strong>Running locally?</strong> The demo company is seeded in Development —
            see the README for the demo login, or <a routerLink="/register">create your own company</a>.
          </div>

          <p class="switch">New here? <a routerLink="/register">Create a company account</a></p>
        </div>
      </section>
    </div>
  `
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>();

  protected readonly busy = signal(false);
  protected readonly showPassword = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  protected submit(): void {
    if (this.form.invalid) return;
    this.busy.set(true);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigateByUrl(this.returnUrl() || '/dashboard'),
      error: () => this.busy.set(false)
    });
  }
}
