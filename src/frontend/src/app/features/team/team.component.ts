import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormGroupDirective, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { TeamMember } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { PageHeaderComponent } from '../../shared/ui';
import { STRONG_PASSWORD } from '../auth/register.component';

@Component({
  selector: 'app-team',
  imports: [ReactiveFormsModule, DatePipe, MatTableModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule, PageHeaderComponent],
  template: `
    <app-page-header title="Team" subtitle="Owners and admins manage the catalogue and can cancel invoices; staff handle day-to-day billing." />

    <section class="card add-user">
      <h2 class="card-title">Add a team member</h2>
      <form [formGroup]="form" (ngSubmit)="add(formDir)" #formDir="ngForm" class="form-grid">
        <mat-form-field>
          <mat-label>Full name</mat-label>
          <input matInput formControlName="fullName">
        </mat-form-field>
        <mat-form-field>
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email">
        </mat-form-field>
        <mat-form-field>
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">
            <mat-option value="Staff">Staff</mat-option>
            <mat-option value="Admin">Admin</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Temporary password</mat-label>
          <input matInput type="password" formControlName="password" autocomplete="new-password">
          <mat-hint>8+ chars, upper, lower, digit</mat-hint>
        </mat-form-field>
        <div class="span-all"><button mat-flat-button [disabled]="form.invalid || busy()"><mat-icon>person_add</mat-icon> Add member</button></div>
      </form>
    </section>

    <div class="table-wrap">
      <table mat-table [dataSource]="members()">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Name</th>
          <td mat-cell *matCellDef="let m"><strong>{{ m.fullName }}</strong><div class="muted">{{ m.email }}</div></td>
        </ng-container>
        <ng-container matColumnDef="role">
          <th mat-header-cell *matHeaderCellDef>Role</th>
          <td mat-cell *matCellDef="let m">{{ m.role }}</td>
        </ng-container>
        <ng-container matColumnDef="since">
          <th mat-header-cell *matHeaderCellDef>Member since</th>
          <td mat-cell *matCellDef="let m">{{ m.createdAtUtc | date: 'dd MMM yyyy' }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let m" class="num">
            @if (!m.isActive) { <span class="muted">Deactivated</span> }
            @else if (m.role !== 'Owner' && m.id !== me?.userId) {
              <button mat-button (click)="deactivate(m)">Deactivate</button>
            }
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns" [class.inactive]="!row.isActive"></tr>
      </table>
    </div>
  `,
  styles: `.add-user { margin-bottom: 16px; }`
})
export class TeamComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotifyService);

  protected readonly me = inject(AuthService).user();
  protected readonly columns = ['name', 'role', 'since', 'actions'];
  protected readonly members = signal<TeamMember[]>([]);
  protected readonly busy = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.maxLength(150)]],
    email: ['', [Validators.required, Validators.email]],
    role: ['Staff' as 'Staff' | 'Admin', Validators.required],
    password: ['', [Validators.required, Validators.pattern(STRONG_PASSWORD)]]
  });

  ngOnInit(): void {
    this.reload();
  }

  protected add(formDir: FormGroupDirective): void {
    if (this.form.invalid) return;
    this.busy.set(true);
    this.api.createUser(this.form.getRawValue()).subscribe({
      next: m => {
        this.notify.success(`${m.fullName} can now sign in`);
        formDir.resetForm({ fullName: '', email: '', role: 'Staff', password: '' });
        this.busy.set(false);
        this.reload();
      },
      error: () => this.busy.set(false)
    });
  }

  protected deactivate(member: TeamMember): void {
    if (!confirm(`Deactivate ${member.fullName}? They will no longer be able to sign in.`)) return;
    this.api.deactivateUser(member.id).subscribe(() => this.reload());
  }

  private reload(): void {
    this.api.users().subscribe(m => this.members.set(m));
  }
}
