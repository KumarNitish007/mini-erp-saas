import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { AuthHeroComponent } from './auth-hero.component';

export const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;
export const GSTIN_PATTERN = /^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$/;

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatProgressSpinnerModule, AuthHeroComponent],
  template: `
    <div class="auth-page">
      <app-auth-hero />
      <section class="auth-panel">
        <div class="auth-card">
          <h1>Create your company</h1>
          <p class="muted">You'll be the owner and can invite your team later.</p>

          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field>
              <mat-label>Company name</mat-label>
              <input matInput formControlName="companyName">
            </mat-form-field>
            <mat-form-field>
              <mat-label>State (place of business)</mat-label>
              <mat-select formControlName="stateCode">
                @for (s of states(); track s.code) { <mat-option [value]="s.code">{{ s.code }} · {{ s.name }}</mat-option> }
              </mat-select>
              <mat-hint>Decides CGST/SGST vs IGST on your invoices</mat-hint>
            </mat-form-field>
            <mat-form-field>
              <mat-label>GSTIN (optional)</mat-label>
              <input matInput formControlName="gstin" class="mono" maxlength="15" (input)="upperGstin()">
              @if (form.controls.gstin.hasError('pattern')) { <mat-error>Enter a valid 15-character GSTIN</mat-error> }
            </mat-form-field>
            <mat-form-field>
              <mat-label>Your name</mat-label>
              <input matInput formControlName="fullName" autocomplete="name">
            </mat-form-field>
            <mat-form-field>
              <mat-label>Work email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="email">
            </mat-form-field>
            <mat-form-field>
              <mat-label>Password</mat-label>
              <input matInput type="password" formControlName="password" autocomplete="new-password">
              <mat-hint>8+ characters with upper, lower case and a digit</mat-hint>
            </mat-form-field>
            <button mat-flat-button class="submit" [disabled]="form.invalid || busy()">
              @if (busy()) { <mat-spinner diameter="20" /> } @else { Create account }
            </button>
          </form>

          <p class="switch">Already have an account? <a routerLink="/login">Sign in</a></p>
        </div>
      </section>
    </div>
  `
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly states = toSignal(inject(ApiService).states(), { initialValue: [] });
  protected readonly busy = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    companyName: ['', [Validators.required, Validators.maxLength(200)]],
    stateCode: ['27', Validators.required],
    gstin: ['', Validators.pattern(GSTIN_PATTERN)],
    fullName: ['', [Validators.required, Validators.maxLength(150)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.pattern(STRONG_PASSWORD)]]
  });

  protected upperGstin(): void {
    const c = this.form.controls.gstin;
    c.setValue(c.value.toUpperCase(), { emitEvent: false });
  }

  protected submit(): void {
    if (this.form.invalid) return;
    this.busy.set(true);
    const v = this.form.getRawValue();
    this.auth.register({ ...v, gstin: v.gstin || null, address: null }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: () => this.busy.set(false)
    });
  }
}
