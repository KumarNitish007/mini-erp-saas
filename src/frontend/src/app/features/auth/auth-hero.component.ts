import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-auth-hero',
  imports: [MatIconModule],
  host: { class: 'auth-hero' },
  template: `
    <div class="hero-brand"><span class="hero-mark">M</span> MiniERP</div>
    <div>
      <h2>Inventory and GST invoicing for growing businesses.</h2>
      <p>Each company gets its own isolated workspace: track stock, bill customers with correct CGST/SGST/IGST, and see who owes you what.</p>
      <ul>
        <li><mat-icon>receipt_long</mat-icon> GST-compliant invoices with gap-free numbering and PDF export</li>
        <li><mat-icon>inventory_2</mat-icon> Stock ledger with low-stock alerts</li>
        <li><mat-icon>insights</mat-icon> Live dashboard: revenue, receivables, overdue</li>
        <li><mat-icon>shield</mat-icon> Multi-tenant by design, with role-based access</li>
      </ul>
    </div>
    <div class="hero-foot">.NET 8 · Angular 19 · SQL Server</div>
  `
})
export class AuthHeroComponent {}
