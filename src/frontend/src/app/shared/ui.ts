import { Component, Pipe, PipeTransform, input } from '@angular/core';
import { InvoiceStatus } from '../core/models';

const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 });
const inrCompact = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', notation: 'compact', maximumFractionDigits: 1 });

/** ₹12,34,567.00 (Indian digit grouping); `| inr:'compact'` → ₹12.3L */
@Pipe({ name: 'inr' })
export class InrPipe implements PipeTransform {
  transform(value: number | null | undefined, mode: 'full' | 'compact' = 'full'): string {
    if (value === null || value === undefined) return '—';
    return mode === 'compact' ? inrCompact.format(value) : inr.format(value);
  }
}

@Component({
  selector: 'app-status-chip',
  template: `<span class="status-chip" [class]="'status-' + cssClass()">{{ label() }}</span>`
})
export class StatusChipComponent {
  readonly status = input.required<InvoiceStatus>();
  readonly overdue = input(false);

  protected cssClass = () => (this.overdue() ? 'overdue' : this.status().toLowerCase());
  protected label = () => (this.overdue() ? 'Overdue' : this.status());
}

@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header">
      <div>
        <h1>{{ title() }}</h1>
        @if (subtitle()) { <p>{{ subtitle() }}</p> }
      </div>
      <div class="page-actions"><ng-content /></div>
    </header>
  `
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
}

/** Today's date in India as yyyy-MM-dd (what <input type="date"> and the API's DateOnly use). */
export function todayIso(offsetDays = 0): string {
  const d = new Date(Date.now() + 5.5 * 3600_000 + offsetDays * 86_400_000);
  return d.toISOString().slice(0, 10);
}

export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
