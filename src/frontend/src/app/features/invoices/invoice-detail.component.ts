import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { InvoiceDetail } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { InrPipe, StatusChipComponent, saveBlob } from '../../shared/ui';

@Component({
  selector: 'app-invoice-detail',
  imports: [RouterLink, DatePipe, MatButtonModule, MatIconModule, MatMenuModule, MatProgressBarModule, InrPipe, StatusChipComponent],
  templateUrl: './invoice-detail.component.html',
  styleUrl: './invoice-detail.component.scss'
})
export class InvoiceDetailComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly notify = inject(NotifyService);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly invoice = signal<InvoiceDetail | null>(null);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    this.api.invoice(this.id()).subscribe(i => this.invoice.set(i));
  }

  protected issue(): void {
    this.run(this.api.invoiceAction(this.id(), 'issue'), i => `Invoice ${i.number} issued — stock updated`);
  }

  protected markPaid(): void {
    this.run(this.api.invoiceAction(this.id(), 'mark-paid'), () => 'Marked as paid');
  }

  protected cancel(): void {
    if (!confirm('Cancel this invoice? Issued stock will be returned to inventory.')) return;
    this.run(this.api.invoiceAction(this.id(), 'cancel'), () => 'Invoice cancelled');
  }

  protected remove(): void {
    if (!confirm('Delete this draft permanently?')) return;
    this.busy.set(true);
    this.api.deleteInvoice(this.id()).subscribe({
      next: () => {
        this.notify.success('Draft deleted');
        this.router.navigate(['/invoices']);
      },
      error: () => this.busy.set(false)
    });
  }

  protected downloadPdf(): void {
    this.busy.set(true);
    this.api.invoicePdf(this.id()).subscribe({
      next: response => {
        const name = /filename="?([^";]+)"?/.exec(response.headers.get('Content-Disposition') ?? '')?.[1] ?? 'invoice.pdf';
        saveBlob(response.body!, name);
        this.busy.set(false);
      },
      error: () => this.busy.set(false)
    });
  }

  private run(call: Observable<InvoiceDetail>, message: (i: InvoiceDetail) => string): void {
    this.busy.set(true);
    call.subscribe({
      next: i => {
        this.invoice.set(i);
        this.busy.set(false);
        this.notify.success(message(i));
      },
      error: () => this.busy.set(false)
    });
  }
}
