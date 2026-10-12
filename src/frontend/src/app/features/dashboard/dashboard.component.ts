import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { Dashboard } from '../../core/models';
import { AuthService } from '../../core/auth.service';
import { InrPipe, PageHeaderComponent } from '../../shared/ui';
import { SalesChartComponent } from './sales-chart.component';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, InrPipe, PageHeaderComponent, SalesChartComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  protected readonly auth = inject(AuthService);
  protected readonly data = toSignal(inject(ApiService).dashboard(6));

  protected totals(d: Dashboard) {
    return d.monthlySales.reduce(
      (t, m) => ({ count: t.count + m.invoiceCount, taxable: t.taxable + m.taxableTotal, tax: t.tax + m.taxTotal, billed: t.billed + m.grandTotal }),
      { count: 0, taxable: 0, tax: 0, billed: 0 }
    );
  }

  protected readonly greeting = (() => {
    const h = new Date().getHours();
    return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
  })();
}
