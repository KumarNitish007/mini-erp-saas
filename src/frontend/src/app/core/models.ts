export type Role = 'Owner' | 'Admin' | 'Staff';
export type InvoiceStatus = 'Draft' | 'Issued' | 'Paid' | 'Cancelled';

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CurrentUser {
  userId: string;
  email: string;
  fullName: string;
  role: Role;
  tenantId: string;
  tenantName: string;
  tenantStateCode: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: CurrentUser;
}

export interface RegisterRequest {
  companyName: string;
  stateCode: string;
  gstin: string | null;
  address: string | null;
  fullName: string;
  email: string;
  password: string;
}

export interface IndianState {
  code: string;
  name: string;
}

export interface Product {
  id: string;
  sku: string;
  name: string;
  unit: string;
  hsnCode: string | null;
  unitPrice: number;
  gstRate: number;
  stockQuantity: number;
  reorderLevel: number;
  isActive: boolean;
  isLowStock: boolean;
}

export interface SaveProduct {
  sku: string;
  name: string;
  unit: string;
  hsnCode: string | null;
  unitPrice: number;
  gstRate: number;
  reorderLevel: number;
  openingStock?: number;
}

export interface StockAdjustment {
  quantity: number;
  type: 'Purchase' | 'Adjustment';
  reference: string | null;
}

export interface StockMovement {
  id: string;
  quantity: number;
  type: string;
  reference: string | null;
  createdAtUtc: string;
}

export interface Customer {
  id: string;
  name: string;
  email: string | null;
  phone: string | null;
  gstin: string | null;
  stateCode: string;
  stateName: string;
  billingAddress: string | null;
  isActive: boolean;
}

export type SaveCustomer = Omit<Customer, 'id' | 'stateName' | 'isActive'>;

export interface InvoiceSummary {
  id: string;
  number: string | null;
  customerId: string;
  customerName: string;
  invoiceDate: string;
  dueDate: string;
  status: InvoiceStatus;
  isOverdue: boolean;
  grandTotal: number;
}

export interface InvoiceLine {
  id: string;
  productId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  gstRate: number;
  taxableAmount: number;
  taxAmount: number;
  lineTotal: number;
}

export interface InvoiceDetail {
  id: string;
  number: string | null;
  customer: Customer;
  invoiceDate: string;
  dueDate: string;
  status: InvoiceStatus;
  isOverdue: boolean;
  isInterState: boolean;
  subTotal: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  grandTotal: number;
  notes: string | null;
  issuedAtUtc: string | null;
  paidAtUtc: string | null;
  lines: InvoiceLine[];
}

export interface SaveInvoice {
  customerId: string;
  invoiceDate: string;
  dueDate: string;
  notes: string | null;
  lines: { productId: string; quantity: number; unitPrice: number | null; description: string | null }[];
}

export interface Dashboard {
  kpis: {
    revenueThisMonth: number;
    outstandingAmount: number;
    overdueAmount: number;
    overdueCount: number;
    draftCount: number;
    lowStockCount: number;
    customerCount: number;
  };
  monthlySales: { monthStart: string; invoiceCount: number; taxableTotal: number; taxTotal: number; grandTotal: number }[];
  lowStock: { id: string; sku: string; name: string; unit: string; stockQuantity: number; reorderLevel: number; shortfall: number }[];
  topCustomers: { id: string; name: string; invoiceCount: number; totalBilled: number; outstanding: number }[];
}

export interface TeamMember {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  isActive: boolean;
  createdAtUtc: string;
}

/** RFC 7807 problem details returned by the API. */
export interface Problem {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export const GST_RATES = [0, 0.25, 3, 5, 12, 18, 28] as const;
