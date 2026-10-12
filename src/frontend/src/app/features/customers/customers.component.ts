import { Component, OnInit, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Customer, Paged } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { PageHeaderComponent } from '../../shared/ui';
import { GSTIN_PATTERN } from '../auth/register.component';

@Component({
  selector: 'app-customer-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ customer ? 'Edit customer' : 'New customer' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field class="span-all">
            <mat-label>Name</mat-label>
            <input matInput formControlName="name" cdkFocusInitial>
          </mat-form-field>
          <mat-form-field>
            <mat-label>State (place of supply)</mat-label>
            <mat-select formControlName="stateCode">
              @for (s of states(); track s.code) { <mat-option [value]="s.code">{{ s.code }} · {{ s.name }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>GSTIN</mat-label>
            <input matInput formControlName="gstin" class="mono" maxlength="15">
            @if (form.controls.gstin.hasError('pattern')) { <mat-error>Not a valid GSTIN</mat-error> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Phone</mat-label>
            <input matInput formControlName="phone">
          </mat-form-field>
          <mat-form-field class="span-all">
            <mat-label>Billing address</mat-label>
            <textarea matInput rows="2" formControlName="billingAddress"></textarea>
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button [disabled]="form.invalid || busy()">Save</button>
      </mat-dialog-actions>
    </form>
  `
})
export class CustomerDialogComponent {
  private readonly api = inject(ApiService);
  private readonly ref = inject(MatDialogRef<CustomerDialogComponent, Customer>);
  private readonly notify = inject(NotifyService);

  protected readonly customer = inject<Customer | null>(MAT_DIALOG_DATA);
  protected readonly states = toSignal(this.api.states(), { initialValue: [] });
  protected readonly busy = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.customer?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    stateCode: [this.customer?.stateCode ?? inject(AuthService).user()?.tenantStateCode ?? '27', Validators.required],
    gstin: [this.customer?.gstin ?? '', Validators.pattern(GSTIN_PATTERN)],
    email: [this.customer?.email ?? '', Validators.email],
    phone: [this.customer?.phone ?? ''],
    billingAddress: [this.customer?.billingAddress ?? '', Validators.maxLength(500)]
  });

  protected save(): void {
    if (this.form.invalid) return;
    this.busy.set(true);
    const v = this.form.getRawValue();
    const body = {
      name: v.name,
      stateCode: v.stateCode,
      gstin: v.gstin.toUpperCase() || null,
      email: v.email || null,
      phone: v.phone || null,
      billingAddress: v.billingAddress || null
    };
    const call = this.customer ? this.api.updateCustomer(this.customer.id, body) : this.api.createCustomer(body);
    call.subscribe({
      next: c => {
        this.notify.success(`Saved ${c.name}`);
        this.ref.close(c);
      },
      error: () => this.busy.set(false)
    });
  }
}

@Component({
  selector: 'app-customers',
  imports: [
    ReactiveFormsModule, RouterLink, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatIconModule, MatButtonModule, MatProgressBarModule, MatTooltipModule, PageHeaderComponent
  ],
  template: `
    <app-page-header title="Customers" subtitle="Who you bill — their state decides CGST/SGST or IGST.">
      <button mat-flat-button (click)="edit(null)"><mat-icon>person_add</mat-icon> New customer</button>
    </app-page-header>

    <div class="toolbar-row">
      <mat-form-field class="search">
        <mat-icon matPrefix>search</mat-icon>
        <mat-label>Search name, email or GSTIN</mat-label>
        <input matInput [formControl]="search">
      </mat-form-field>
    </div>

    <div class="table-wrap">
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <table mat-table [dataSource]="page()?.items ?? []">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Name</th>
          <td mat-cell *matCellDef="let c"><strong>{{ c.name }}</strong><div class="muted">{{ c.email }}</div></td>
        </ng-container>
        <ng-container matColumnDef="state">
          <th mat-header-cell *matHeaderCellDef>State</th>
          <td mat-cell *matCellDef="let c">
            {{ c.stateName }}
            @if (c.stateCode !== homeState) { <span class="muted" matTooltip="Inter-state: IGST applies">· IGST</span> }
          </td>
        </ng-container>
        <ng-container matColumnDef="gstin">
          <th mat-header-cell *matHeaderCellDef>GSTIN</th>
          <td mat-cell *matCellDef="let c" class="mono">{{ c.gstin ?? '—' }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let c" class="num">
            <a mat-icon-button routerLink="/invoices" [queryParams]="{ customerId: c.id }" matTooltip="Invoices"><mat-icon>receipt_long</mat-icon></a>
            <button mat-icon-button (click)="edit(c)" matTooltip="Edit"><mat-icon>edit</mat-icon></button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
      @if (!loading() && page()?.totalCount === 0) {
        <div class="empty-state"><mat-icon>groups</mat-icon><p>No customers yet.</p></div>
      }
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="(page()?.page ?? 1) - 1" [pageSize]="pageSize"
                     [pageSizeOptions]="[10, 20, 50]" (page)="onPage($event)" />
    </div>
  `
})
export class CustomersComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);

  protected readonly homeState = inject(AuthService).user()?.tenantStateCode;
  protected readonly columns = ['name', 'state', 'gstin', 'actions'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly page = signal<Paged<Customer> | null>(null);
  protected readonly loading = signal(false);
  protected pageSize = 20;

  ngOnInit(): void {
    this.search.valueChanges.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => this.reload(1));
    this.reload(1);
  }

  protected reload(pageNumber = this.page()?.page ?? 1): void {
    this.loading.set(true);
    this.api.customers({ page: pageNumber, pageSize: this.pageSize, search: this.search.value })
      .subscribe({ next: p => { this.page.set(p); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  protected onPage(e: PageEvent): void {
    this.pageSize = e.pageSize;
    this.reload(e.pageIndex + 1);
  }

  protected edit(customer: Customer | null): void {
    this.dialog.open(CustomerDialogComponent, { data: customer, width: '640px' })
      .afterClosed().subscribe(saved => saved && this.reload());
  }
}
