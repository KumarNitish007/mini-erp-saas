import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ApiService } from '../../core/api.service';
import { GST_RATES, Product, SaveProduct } from '../../core/models';
import { NotifyService } from '../../core/notify.service';

@Component({
  selector: 'app-product-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ product ? 'Edit product' : 'New product' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field>
            <mat-label>SKU</mat-label>
            <input matInput formControlName="sku" class="mono" [readonly]="!!product" cdkFocusInitial>
            @if (product) { <mat-hint>SKU is permanent</mat-hint> }
          </mat-form-field>
          <mat-form-field>
            <mat-label>HSN code</mat-label>
            <input matInput formControlName="hsnCode" class="mono" maxlength="8">
          </mat-form-field>
          <mat-form-field class="span-all">
            <mat-label>Name</mat-label>
            <input matInput formControlName="name">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Unit</mat-label>
            <input matInput formControlName="unit" placeholder="pcs, kg, bag…">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Unit price (₹, excl. GST)</mat-label>
            <input matInput type="number" min="0" step="0.01" formControlName="unitPrice">
          </mat-form-field>
          <mat-form-field>
            <mat-label>GST rate</mat-label>
            <mat-select formControlName="gstRate">
              @for (r of rates; track r) { <mat-option [value]="r">{{ r }}%</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Reorder level</mat-label>
            <input matInput type="number" min="0" formControlName="reorderLevel">
          </mat-form-field>
          @if (!product) {
            <mat-form-field>
              <mat-label>Opening stock</mat-label>
              <input matInput type="number" min="0" formControlName="openingStock">
            </mat-form-field>
          }
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button [disabled]="form.invalid || busy()">Save</button>
      </mat-dialog-actions>
    </form>
  `
})
export class ProductDialogComponent {
  private readonly api = inject(ApiService);
  private readonly ref = inject(MatDialogRef<ProductDialogComponent, Product>);
  private readonly notify = inject(NotifyService);

  protected readonly product = inject<Product | null>(MAT_DIALOG_DATA);
  protected readonly rates = GST_RATES;
  protected readonly busy = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    sku: [this.product?.sku ?? '', [Validators.required, Validators.maxLength(50), Validators.pattern(/^[A-Za-z0-9._-]+$/)]],
    name: [this.product?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    unit: [this.product?.unit ?? 'pcs', [Validators.required, Validators.maxLength(20)]],
    hsnCode: [this.product?.hsnCode ?? '', Validators.pattern(/^[0-9]{4,8}$/)],
    unitPrice: [this.product?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
    gstRate: [this.product?.gstRate ?? 18, Validators.required],
    reorderLevel: [this.product?.reorderLevel ?? 0, [Validators.required, Validators.min(0)]],
    openingStock: [0, Validators.min(0)]
  });

  protected save(): void {
    if (this.form.invalid) return;
    this.busy.set(true);
    const v = this.form.getRawValue();
    const body: SaveProduct = { ...v, hsnCode: v.hsnCode || null };
    const call = this.product ? this.api.updateProduct(this.product.id, body) : this.api.createProduct(body);
    call.subscribe({
      next: p => {
        this.notify.success(`Saved ${p.sku}`);
        this.ref.close(p);
      },
      error: () => this.busy.set(false)
    });
  }
}

@Component({
  selector: 'app-stock-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatButtonToggleModule],
  template: `
    <h2 mat-dialog-title>Adjust stock</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <p class="muted">{{ product.name }} · current stock <strong>{{ product.stockQuantity }} {{ product.unit }}</strong></p>
        <mat-button-toggle-group formControlName="direction" class="full-width" style="margin-bottom: 16px">
          <mat-button-toggle value="in" class="full-width">Stock in (purchase)</mat-button-toggle>
          <mat-button-toggle value="out" class="full-width">Stock out (adjustment)</mat-button-toggle>
        </mat-button-toggle-group>
        <div class="form-grid">
          <mat-form-field>
            <mat-label>Quantity ({{ product.unit }})</mat-label>
            <input matInput type="number" min="0" formControlName="quantity" cdkFocusInitial>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Reference</mat-label>
            <input matInput formControlName="reference" placeholder="PO / GRN / reason">
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button [disabled]="form.invalid || busy()">Apply</button>
      </mat-dialog-actions>
    </form>
  `
})
export class StockDialogComponent {
  private readonly api = inject(ApiService);
  private readonly ref = inject(MatDialogRef<StockDialogComponent, Product>);
  private readonly notify = inject(NotifyService);

  protected readonly product = inject<Product>(MAT_DIALOG_DATA);
  protected readonly busy = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    direction: ['in' as 'in' | 'out'],
    quantity: [0, [Validators.required, Validators.min(0.001)]],
    reference: ['', Validators.maxLength(100)]
  });

  protected save(): void {
    if (this.form.invalid) return;
    this.busy.set(true);
    const { direction, quantity, reference } = this.form.getRawValue();
    this.api
      .adjustStock(this.product.id, {
        quantity: direction === 'in' ? quantity : -quantity,
        type: direction === 'in' ? 'Purchase' : 'Adjustment',
        reference: reference || null
      })
      .subscribe({
        next: p => {
          this.notify.success(`Stock for ${p.sku} is now ${p.stockQuantity} ${p.unit}`);
          this.ref.close(p);
        },
        error: () => this.busy.set(false)
      });
  }
}
