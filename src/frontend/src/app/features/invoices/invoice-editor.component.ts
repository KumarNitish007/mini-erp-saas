import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, map, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { invoiceTotals, lineAmounts } from '../../core/gst';
import { Customer, InvoiceDetail, Product, SaveInvoice } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { InrPipe, PageHeaderComponent, todayIso } from '../../shared/ui';

type LineForm = FormGroup<{
  productId: FormControl<string>;
  quantity: FormControl<number>;
  unitPrice: FormControl<number>;
  description: FormControl<string>;
}>;

@Component({
  selector: 'app-invoice-editor',
  imports: [
    ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule,
    MatIconModule, MatTooltipModule, MatProgressBarModule, InrPipe, PageHeaderComponent
  ],
  templateUrl: './invoice-editor.component.html',
  styleUrl: './invoice-editor.component.scss'
})
export class InvoiceEditorComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly tenantState = inject(AuthService).user()?.tenantStateCode;

  /** Route param when editing an existing draft. */
  readonly id = input<string>();

  protected readonly customers = signal<Customer[]>([]);
  protected readonly products = signal<Product[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);

  protected readonly form = this.fb.group({
    customerId: ['', Validators.required],
    invoiceDate: [todayIso(), Validators.required],
    dueDate: [todayIso(30), Validators.required],
    notes: ['', Validators.maxLength(1000)],
    lines: this.fb.array<LineForm>([])
  });

  private readonly formValue = toSignal(this.form.valueChanges.pipe(startWith(null), map(() => this.form.getRawValue())));

  protected readonly selectedCustomer = computed(() => this.customers().find(c => c.id === this.formValue()?.customerId));
  protected readonly isInterState = computed(() => !!this.selectedCustomer() && this.selectedCustomer()!.stateCode !== this.tenantState);

  protected readonly preview = computed(() => {
    const productMap = new Map(this.products().map(p => [p.id, p]));
    const lines = (this.formValue()?.lines ?? []).map(l => ({
      quantity: Number(l.quantity),
      unitPrice: Number(l.unitPrice),
      gstRate: productMap.get(l.productId)?.gstRate ?? 0
    }));
    return { lines: lines.map(lineAmounts), totals: invoiceTotals(lines, this.isInterState()) };
  });

  get lines(): FormArray<LineForm> {
    return this.form.controls.lines;
  }

  ngOnInit(): void {
    forkJoin({
      customers: this.api.customers({ pageSize: 100 }),
      products: this.api.products({ pageSize: 100 })
    })
      .pipe(switchMap(r => {
        this.customers.set(r.customers.items);
        this.products.set(r.products.items);
        return this.id() ? this.api.invoice(this.id()!) : [null];
      }))
      .subscribe({
        next: invoice => {
          if (invoice) this.load(invoice);
          else this.addLine();
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      });
  }

  protected addLine(productId = '', quantity = 1, unitPrice = 0, description = ''): void {
    this.lines.push(this.fb.group({
      productId: [productId, Validators.required],
      quantity: [quantity, [Validators.required, Validators.min(0.001)]],
      unitPrice: [unitPrice, [Validators.required, Validators.min(0)]],
      description: [description, Validators.maxLength(300)]
    }));
  }

  protected removeLine(index: number): void {
    this.lines.removeAt(index);
  }

  protected productChanged(line: LineForm): void {
    const product = this.products().find(p => p.id === line.controls.productId.value);
    if (product) line.controls.unitPrice.setValue(product.unitPrice);
  }

  protected productOf(line: LineForm): Product | undefined {
    return this.products().find(p => p.id === line.controls.productId.value);
  }

  protected save(issue: boolean): void {
    if (this.form.invalid || this.lines.length === 0) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    const v = this.form.getRawValue();
    const body: SaveInvoice = {
      customerId: v.customerId,
      invoiceDate: v.invoiceDate,
      dueDate: v.dueDate,
      notes: v.notes || null,
      lines: v.lines.map(l => ({
        productId: l.productId,
        quantity: Number(l.quantity),
        unitPrice: Number(l.unitPrice),
        description: l.description || null
      }))
    };

    const save$ = this.id() ? this.api.updateInvoice(this.id()!, body) : this.api.createInvoice(body);
    save$
      .pipe(switchMap(draft => (issue ? this.api.invoiceAction(draft.id, 'issue') : [draft])))
      .subscribe({
        next: invoice => {
          this.notify.success(issue ? `Invoice ${invoice.number} issued` : 'Draft saved');
          this.router.navigate(['/invoices', invoice.id]);
        },
        error: () => this.busy.set(false)
      });
  }

  private load(invoice: InvoiceDetail): void {
    if (invoice.status !== 'Draft') {
      this.notify.error('Only drafts can be edited.');
      this.router.navigate(['/invoices', invoice.id]);
      return;
    }
    if (!this.customers().some(c => c.id === invoice.customer.id)) this.customers.update(c => [invoice.customer, ...c]);
    this.form.patchValue({
      customerId: invoice.customer.id,
      invoiceDate: invoice.invoiceDate,
      dueDate: invoice.dueDate,
      notes: invoice.notes ?? ''
    });
    this.form.controls.customerId.disable();
    invoice.lines.forEach(l => this.addLine(l.productId, l.quantity, l.unitPrice, l.description));
  }
}
