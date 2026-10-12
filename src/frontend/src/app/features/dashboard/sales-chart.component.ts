import { Component, computed, input } from '@angular/core';
import { InrPipe } from '../../shared/ui';

export interface SalesPoint {
  monthStart: string;
  grandTotal: number;
  invoiceCount: number;
}

/** Dependency-free SVG bar chart of monthly billing. */
@Component({
  selector: 'app-sales-chart',
  imports: [InrPipe],
  template: `
    <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" class="chart" role="img" aria-label="Monthly sales">
      @for (g of gridLines(); track g.y) {
        <line [attr.x1]="padLeft" [attr.x2]="width" [attr.y1]="g.y" [attr.y2]="g.y" class="grid" />
        <text [attr.x]="padLeft - 8" [attr.y]="g.y + 4" class="axis" text-anchor="end">{{ g.value | inr: 'compact' }}</text>
      }
      @for (b of bars(); track b.label) {
        <g>
          <title>{{ b.label }}: {{ b.value | inr }} ({{ b.count }} invoices)</title>
          <rect [attr.x]="b.x" [attr.y]="b.y" [attr.width]="b.w" [attr.height]="b.h" rx="6" class="bar" [class.current]="$last" />
          <text [attr.x]="b.x + b.w / 2" [attr.y]="height - 6" class="axis" text-anchor="middle">{{ b.label }}</text>
        </g>
      }
    </svg>
  `,
  styles: `
    .chart { width: 100%; height: auto; display: block; }
    .grid { stroke: var(--mat-sys-outline-variant); stroke-dasharray: 3 4; }
    .axis { fill: var(--mat-sys-on-surface-variant); font-size: 11px; }
    .bar { fill: var(--mat-sys-primary-container); transition: fill 0.2s; }
    .bar.current, .bar:hover { fill: var(--mat-sys-primary); }
  `
})
export class SalesChartComponent {
  readonly data = input.required<SalesPoint[]>();

  protected readonly width = 640;
  protected readonly height = 330;
  protected readonly padLeft = 56;
  private readonly padBottom = 26;
  private readonly padTop = 10;

  private readonly max = computed(() => {
    const m = Math.max(...this.data().map(d => d.grandTotal), 1);
    const magnitude = 10 ** Math.floor(Math.log10(m));
    return Math.ceil(m / magnitude) * magnitude;
  });

  protected readonly gridLines = computed(() => {
    const plot = this.height - this.padBottom - this.padTop;
    return [0, 0.25, 0.5, 0.75, 1].map(f => ({ y: this.padTop + plot * (1 - f), value: this.max() * f }));
  });

  protected readonly bars = computed(() => {
    const points = this.data();
    const plot = this.height - this.padBottom - this.padTop;
    const slot = (this.width - this.padLeft) / Math.max(points.length, 1);
    const w = Math.min(46, slot * 0.6);
    return points.map((p, i) => {
      const h = Math.max(2, (p.grandTotal / this.max()) * plot);
      return {
        x: this.padLeft + slot * i + (slot - w) / 2,
        y: this.padTop + plot - h,
        w,
        h,
        value: p.grandTotal,
        count: p.invoiceCount,
        label: new Date(p.monthStart).toLocaleDateString('en-IN', { month: 'short' })
      };
    });
  });
}
