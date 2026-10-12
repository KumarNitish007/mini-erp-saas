import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../core/auth.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  managerOnly?: boolean;
}

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, MatSidenavModule, MatToolbarModule, MatListModule,
    MatIconModule, MatButtonModule, MatMenuModule, MatTooltipModule
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe('(max-width: 960px)').pipe(map(r => r.matches)),
    { initialValue: false }
  );

  protected readonly dark = signal(document.documentElement.style.colorScheme === 'dark');

  private readonly items: NavItem[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'space_dashboard' },
    { path: '/invoices', label: 'Invoices', icon: 'receipt_long' },
    { path: '/products', label: 'Products & stock', icon: 'inventory_2' },
    { path: '/customers', label: 'Customers', icon: 'groups' },
    { path: '/team', label: 'Team', icon: 'admin_panel_settings', managerOnly: true }
  ];

  protected readonly nav = computed(() => this.items.filter(i => !i.managerOnly || this.auth.isManager()));

  protected readonly initials = computed(() =>
    (this.auth.user()?.fullName ?? '?').split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase()
  );

  protected toggleTheme(): void {
    this.dark.update(d => !d);
    document.documentElement.style.colorScheme = this.dark() ? 'dark' : 'light';
  }
}
