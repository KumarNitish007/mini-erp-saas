import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import {
  AuthResponse, CurrentUser, Customer, Dashboard, IndianState, InvoiceDetail, InvoiceSummary, Paged, Product,
  RegisterRequest, SaveCustomer, SaveInvoice, SaveProduct, StockAdjustment, StockMovement, TeamMember
} from './models';

type Query = Record<string, string | number | boolean | null | undefined>;

function toParams(query: Query): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value));
  }
  return params;
}

/** Thin, typed wrapper over the MiniERP REST API (served under /api). */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  private states$?: Observable<IndianState[]>;

  // ---- auth
  login(email: string, password: string) {
    return this.http.post<AuthResponse>(`${this.base}/auth/login`, { email, password });
  }
  register(request: RegisterRequest) {
    return this.http.post<AuthResponse>(`${this.base}/auth/register`, request);
  }
  me() {
    return this.http.get<CurrentUser>(`${this.base}/auth/me`);
  }
  states() {
    return (this.states$ ??= this.http.get<IndianState[]>(`${this.base}/lookups/states`).pipe(shareReplay(1)));
  }

  // ---- dashboard
  dashboard(months = 6) {
    return this.http.get<Dashboard>(`${this.base}/dashboard`, { params: toParams({ months }) });
  }

  // ---- products
  products(query: Query = {}) {
    return this.http.get<Paged<Product>>(`${this.base}/products`, { params: toParams(query) });
  }
  createProduct(body: SaveProduct) {
    return this.http.post<Product>(`${this.base}/products`, body);
  }
  updateProduct(id: string, body: SaveProduct) {
    return this.http.put<Product>(`${this.base}/products/${id}`, body);
  }
  adjustStock(id: string, body: StockAdjustment) {
    return this.http.post<Product>(`${this.base}/products/${id}/stock`, body);
  }
  movements(id: string) {
    return this.http.get<StockMovement[]>(`${this.base}/products/${id}/movements`);
  }
  setProductActive(id: string, active: boolean) {
    return this.http.post<void>(`${this.base}/products/${id}/${active ? 'activate' : 'deactivate'}`, null);
  }

  // ---- customers
  customers(query: Query = {}) {
    return this.http.get<Paged<Customer>>(`${this.base}/customers`, { params: toParams(query) });
  }
  createCustomer(body: SaveCustomer) {
    return this.http.post<Customer>(`${this.base}/customers`, body);
  }
  updateCustomer(id: string, body: SaveCustomer) {
    return this.http.put<Customer>(`${this.base}/customers/${id}`, body);
  }
  deactivateCustomer(id: string) {
    return this.http.post<void>(`${this.base}/customers/${id}/deactivate`, null);
  }

  // ---- invoices
  invoices(query: Query = {}) {
    return this.http.get<Paged<InvoiceSummary>>(`${this.base}/invoices`, { params: toParams(query) });
  }
  invoice(id: string) {
    return this.http.get<InvoiceDetail>(`${this.base}/invoices/${id}`);
  }
  createInvoice(body: SaveInvoice) {
    return this.http.post<InvoiceDetail>(`${this.base}/invoices`, body);
  }
  updateInvoice(id: string, body: SaveInvoice) {
    return this.http.put<InvoiceDetail>(`${this.base}/invoices/${id}`, body);
  }
  deleteInvoice(id: string) {
    return this.http.delete<void>(`${this.base}/invoices/${id}`);
  }
  invoiceAction(id: string, action: 'issue' | 'mark-paid' | 'cancel') {
    return this.http.post<InvoiceDetail>(`${this.base}/invoices/${id}/${action}`, null);
  }
  invoicePdf(id: string) {
    return this.http.get(`${this.base}/invoices/${id}/pdf`, { responseType: 'blob', observe: 'response' });
  }

  // ---- team
  users() {
    return this.http.get<TeamMember[]>(`${this.base}/users`);
  }
  createUser(body: { email: string; fullName: string; role: 'Admin' | 'Staff'; password: string }) {
    return this.http.post<TeamMember>(`${this.base}/users`, body);
  }
  deactivateUser(id: string) {
    return this.http.post<void>(`${this.base}/users/${id}/deactivate`, null);
  }
}
