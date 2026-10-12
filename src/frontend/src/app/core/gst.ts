/**
 * Client-side GST preview. Mirrors MiniErp.Domain.Sales.GstCalculator so the invoice editor
 * shows the same numbers the server will store (the server remains the source of truth).
 */
export interface GstLine {
  quantity: number;
  unitPrice: number;
  gstRate: number;
}

export interface GstTotals {
  subTotal: number;
  cgst: number;
  sgst: number;
  igst: number;
  grandTotal: number;
}

/** Round half away from zero to 2 decimals, matching .NET MidpointRounding.AwayFromZero. */
export function round2(value: number): number {
  // toPrecision(15) strips binary noise first (1.005 * 100 = 100.49999999999999 → 100.5).
  const cents = Number((Math.abs(value) * 100).toPrecision(15));
  return (Math.sign(value) * Math.round(cents)) / 100;
}

export function lineAmounts(line: GstLine): { taxable: number; tax: number; total: number } {
  const taxable = round2((line.quantity || 0) * (line.unitPrice || 0));
  const tax = round2((taxable * (line.gstRate || 0)) / 100);
  return { taxable, tax, total: round2(taxable + tax) };
}

export function invoiceTotals(lines: GstLine[], isInterState: boolean): GstTotals {
  let subTotal = 0;
  let tax = 0;
  for (const l of lines) {
    const a = lineAmounts(l);
    subTotal = round2(subTotal + a.taxable);
    tax = round2(tax + a.tax);
  }
  if (isInterState) return { subTotal, cgst: 0, sgst: 0, igst: tax, grandTotal: round2(subTotal + tax) };

  const cgst = round2(tax / 2);
  return { subTotal, cgst, sgst: round2(tax - cgst), igst: 0, grandTotal: round2(subTotal + tax) };
}
