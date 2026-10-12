import { Routes } from '@angular/router';
import { authGuard, guestGuard, managerGuard } from './core/http';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · MiniERP',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: 'Create your company · MiniERP',
    loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.component').then(m => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · MiniERP',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'invoices',
        title: 'Invoices · MiniERP',
        loadComponent: () => import('./features/invoices/invoices.component').then(m => m.InvoicesComponent)
      },
      {
        path: 'invoices/new',
        title: 'New invoice · MiniERP',
        loadComponent: () => import('./features/invoices/invoice-editor.component').then(m => m.InvoiceEditorComponent)
      },
      {
        path: 'invoices/:id/edit',
        title: 'Edit invoice · MiniERP',
        loadComponent: () => import('./features/invoices/invoice-editor.component').then(m => m.InvoiceEditorComponent)
      },
      {
        path: 'invoices/:id',
        title: 'Invoice · MiniERP',
        loadComponent: () => import('./features/invoices/invoice-detail.component').then(m => m.InvoiceDetailComponent)
      },
      {
        path: 'products',
        title: 'Products · MiniERP',
        loadComponent: () => import('./features/products/products.component').then(m => m.ProductsComponent)
      },
      {
        path: 'customers',
        title: 'Customers · MiniERP',
        loadComponent: () => import('./features/customers/customers.component').then(m => m.CustomersComponent)
      },
      {
        path: 'team',
        title: 'Team · MiniERP',
        canActivate: [managerGuard],
        loadComponent: () => import('./features/team/team.component').then(m => m.TeamComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
