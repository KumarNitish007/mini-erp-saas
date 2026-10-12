import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Paged, Product } from '../../core/models';
import { InrPipe, PageHeaderComponent } from '../../shared/ui';
import { ProductDialogComponent, StockDialogComponent } from './product-dialogs';

@Component({
  selector: 'app-products',
  imports: [
    ReactiveFormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule, MatIconModule,
    MatButtonModule, MatMenuModule, MatSlideToggleModule, MatProgressBarModule, InrPipe, PageHeaderComponent
  ],
  template: `
    <app-page-header title="Products & stock" subtitle="Your catalogue, prices, GST slabs and live stock levels.">
      <button mat-flat-button (click)="edit(null)"><mat-icon>add</mat-icon> New product</button>
    </app-page-header>

    <div class="toolbar-row">
      <mat-form-field class="search">
        <mat-icon matPrefix>search</mat-icon>
        <mat-label>Search name, SKU or HSN</mat-label>
        <input matInput [formControl]="search">
      </mat-form-field>
      <mat-slide-toggle [checked]="lowOnly()" (change)="lowOnly.set($event.checked); reload(1)">Low stock only</mat-slide-toggle>
      <mat-slide-toggle [checked]="showInactive()" (change)="showInactive.set($event.checked); reload(1)">Show inactive</mat-slide-toggle>
    </div>

    <div class="table-wrap">
      @if (loading()) { <mat-progress-bar mode="indeterminate" /> }
      <table mat-table [dataSource]="page()?.items ?? []">
        <ng-container matColumnDef="sku">
          <th mat-header-cell *matHeaderCellDef>SKU</th>
          <td mat-cell *matCellDef="let p" class="mono">{{ p.sku }}</td>
        </ng-container>
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Name</th>
          <td mat-cell *matCellDef="let p">{{ p.name }} @if (p.hsnCode) { <span class="muted mono">· HSN {{ p.hsnCode }}</span> }</td>
        </ng-container>
        <ng-container matColumnDef="price">
          <th mat-header-cell *matHeaderCellDef class="num">Price</th>
          <td mat-cell *matCellDef="let p" class="num">{{ p.unitPrice | inr }} <span class="muted">/ {{ p.unit }}</span></td>
        </ng-container>
        <ng-container matColumnDef="gst">
          <th mat-header-cell *matHeaderCellDef class="num">GST</th>
          <td mat-cell *matCellDef="let p" class="num">{{ p.gstRate }}%</td>
        </ng-container>
        <ng-container matColumnDef="stock">
          <th mat-header-cell *matHeaderCellDef class="num">Stock</th>
          <td mat-cell *matCellDef="let p" class="num">
            @if (p.isLowStock) {
              <span class="badge-low"><mat-icon inline>warning_amber</mat-icon>{{ p.stockQuantity }}</span>
            } @else { {{ p.stockQuantity }} }
            <span class="muted">&nbsp;{{ p.unit }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let p" class="num">
            <button mat-icon-button [matMenuTriggerFor]="menu" aria-label="Actions"><mat-icon>more_vert</mat-icon></button>
            <mat-menu #menu="matMenu">
              <button mat-menu-item (click)="edit(p)"><mat-icon>edit</mat-icon> Edit</button>
              <button mat-menu-item (click)="adjust(p)"><mat-icon>swap_vert</mat-icon> Adjust stock</button>
              @if (auth.isManager()) {
                <button mat-menu-item (click)="toggleActive(p)">
                  <mat-icon>{{ p.isActive ? 'block' : 'check_circle' }}</mat-icon> {{ p.isActive ? 'Deactivate' : 'Activate' }}
                </button>
              }
            </mat-menu>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns" [class.inactive]="!row.isActive"></tr>
      </table>
      @if (!loading() && page()?.totalCount === 0) {
        <div class="empty-state"><mat-icon>inventory_2</mat-icon><p>No products found.</p></div>
      }
      <mat-paginator [length]="page()?.totalCount ?? 0" [pageIndex]="(page()?.page ?? 1) - 1" [pageSize]="pageSize"
                     [pageSizeOptions]="[10, 20, 50]" (page)="onPage($event)" />
    </div>
  `
})
export class ProductsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  protected readonly auth = inject(AuthService);

  /** Bound from ?lowStock=true (dashboard link). */
  readonly lowStock = input<string>();

  protected readonly columns = ['sku', 'name', 'price', 'gst', 'stock', 'actions'];
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly page = signal<Paged<Product> | null>(null);
  protected readonly loading = signal(false);
  protected readonly lowOnly = signal(false);
  protected readonly showInactive = signal(false);
  protected pageSize = 20;

  ngOnInit(): void {
    this.lowOnly.set(this.lowStock() === 'true');
    this.search.valueChanges.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => this.reload(1));
    this.reload(1);
  }

  protected reload(pageNumber = this.page()?.page ?? 1): void {
    this.loading.set(true);
    this.api
      .products({
        page: pageNumber,
        pageSize: this.pageSize,
        search: this.search.value,
        lowStockOnly: this.lowOnly(),
        includeInactive: this.showInactive()
      })
      .subscribe({ next: p => { this.page.set(p); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  protected onPage(e: PageEvent): void {
    this.pageSize = e.pageSize;
    this.reload(e.pageIndex + 1);
  }

  protected edit(product: Product | null): void {
    this.dialog.open(ProductDialogComponent, { data: product, width: '640px' })
      .afterClosed().subscribe(saved => saved && this.reload());
  }

  protected adjust(product: Product): void {
    this.dialog.open(StockDialogComponent, { data: product, width: '520px' })
      .afterClosed().subscribe(saved => saved && this.reload());
  }

  protected toggleActive(product: Product): void {
    this.api.setProductActive(product.id, !product.isActive).subscribe(() => this.reload());
  }
}
