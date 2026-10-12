import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { InvoiceSummary, Paged } from '../../core/models';
import { InrPipe, PageHeaderComponent, StatusChipComponent } from '../../shared/ui';

type Filter = 'All' | 'Draft' | 'Issued' | 'Overdue' | 'Paid' | 'Cancelled';

@Component({
  selector: 'app-invoices',
  imports: [
    ReactiveFormsModule, RouterLink, DatePipe, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatIconModule, MatButtonModule, MatChipsModule, MatProgressBarModule, InrPipe, PageHeaderComponent, StatusChipComponent
  ],
  template: `
    <app-page-header title="Invoices" subtitle="Drafts are editable; issuing assigns the number and takes stock out.">
      <a mat-flat-button routerLink="/invoices/new"><mat-icon>add</mat-icon> New invoice</a>
    </app-page-header>

    <div class="toolbar-row">
      <mat-form-field class="search">
        <mat-icon matPrefix>search</mat-icon>
        <mat-label>Search number or customer</mat-label>
        <input matInput [formControl]="search">
      </mat-form-field>
      <mat-chip-listbox [value]="filter()" (change)="setFilter($event.value)" aria-label="Status filter">
        @for (f of filters; track f) { <mat-chip-option [value]="f">{{ f }}</mat-chip-option> }
      </mat-chip-listbox>
      @if (customerId()) {
        <a mat-button routerLink="/invoices"><mat-icon>close</mat-icon> Clear customer filter</a>
      }
    </div>

    <div class="table-wrap">
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <table mat-table [dataSource]="page()?.items ?? []">
        <ng-container matColumnDef="number">
          <th mat-header-cell *matHeaderCellDef>Number</th>
          <td mat-cell *matCellDef="let i" class="mono">{{ i.number ?? 'Draft' }}</td>
        </ng-container>
        <ng-container matColumnDef="customer">
          <th mat-header-cell *matHeaderCellDef>Customer</th>
          <td mat-cell *matCellDef="let i">{{ i.customerName }}</td>
        </ng-container>
        <ng-container matColumnDef="date">
          <th mat-header-cell *matHeaderCellDef>Date</th>
          <td mat-cell *matCellDef="let i">{{ i.invoiceDate | date: 'dd MMM yyyy' }}</td>
        </ng-container>
        <ng-container matColumnDef="due">
          <th mat-header-cell *matHeaderCellDef>Due</th>
          <td mat-cell *matCellDef="let i" [class.overdue-text]="i.isOverdue">{{ i.dueDate | date: 'dd MMM yyyy' }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Status</th>
          <td mat-cell *matCellDef="let i"><app-status-chip [status]="i.status" [overdue]="i.isOverdue" /></td>
        </ng-container>
        <ng-container matColumnDef="total">
          <th mat-header-cell *matHeaderCellDef class="num">Total</th>
          <td mat-cell *matCellDef="let i" class="num"><strong>{{ i.grandTotal | inr }}</strong></td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns" class="clickable" (click)="open(row)"></tr>
      </table>
      @if (!loading() && page()?.totalCount === 0) {
        <div class="empty-state"><mat-icon>receipt_long</mat-icon><p>No invoices match.</p></div>
      }
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="(page()?.page ?? 1) - 1" [pageSize]="pageSize"
                     [pageSizeOptions]="[10, 20, 50]" (page)="onPage($event)" />
    </div>
  `,
  styles: `.overdue-text { color: var(--mat-sys-error); font-weight: 600; }`
})
export class InvoicesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  // Query-string filters (from dashboard / customer links).
  readonly status = input<string>();
  readonly overdue = input<string>();
  readonly customerId = input<string>();

  protected readonly filters: Filter[] = ['All', 'Draft', 'Issued', 'Overdue', 'Paid', 'Cancelled'];
  protected readonly columns = ['number', 'customer', 'date', 'due', 'status', 'total'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly filter = signal<Filter>('All');
  protected readonly page = signal<Paged<InvoiceSummary> | null>(null);
  protected readonly loading = signal(false);
  protected pageSize = 20;

  ngOnInit(): void {
    if (this.overdue() === 'true') this.filter.set('Overdue');
    else if (this.filters.includes(this.status() as Filter)) this.filter.set(this.status() as Filter);

    this.search.valueChanges.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => this.reload(1));
    this.reload(1);
  }

  protected setFilter(value: Filter | undefined): void {
    this.filter.set(value ?? 'All');
    this.reload(1);
  }

  protected reload(pageNumber = this.page()?.page ?? 1): void {
    const f = this.filter();
    this.loading.set(true);
    this.api
      .invoices({
        page: pageNumber,
        pageSize: this.pageSize,
        search: this.search.value,
        status: f === 'All' || f === 'Overdue' ? null : f,
        overdueOnly: f === 'Overdue' || null,
        customerId: this.customerId()
      })
      .subscribe({ next: p => { this.page.set(p); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  protected onPage(e: PageEvent): void {
    this.pageSize = e.pageSize;
    this.reload(e.pageIndex + 1);
  }

  protected open(invoice: InvoiceSummary): void {
    this.router.navigate(['/invoices', invoice.id]);
  }
}
