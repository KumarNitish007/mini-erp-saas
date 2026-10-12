import { invoiceTotals, lineAmounts, round2 } from './gst';

describe('GST preview', () => {
  it('rounds half away from zero like the server', () => {
    expect(round2(0.025)).toBe(0.03);
    expect(round2(1.005)).toBe(1.01);
    expect(round2(-0.025)).toBe(-0.03);
  });

  it('calculates a line', () => {
    expect(lineAmounts({ quantity: 3, unitPrice: 33.33, gstRate: 18 })).toEqual({ taxable: 99.99, tax: 18, total: 117.99 });
  });

  it('splits intra-state tax into CGST + SGST', () => {
    const t = invoiceTotals([{ quantity: 1, unitPrice: 1000, gstRate: 18 }], false);
    expect(t).toEqual({ subTotal: 1000, cgst: 90, sgst: 90, igst: 0, grandTotal: 1180 });
  });

  it('charges IGST for inter-state supply', () => {
    const t = invoiceTotals([{ quantity: 2, unitPrice: 500, gstRate: 12 }], true);
    expect(t).toEqual({ subTotal: 1000, cgst: 0, sgst: 0, igst: 120, grandTotal: 1120 });
  });

  it('keeps CGST + SGST equal to the total tax when paise are odd', () => {
    const t = invoiceTotals([{ quantity: 1, unitPrice: 1, gstRate: 5 }], false);
    expect(t.cgst + t.sgst).toBeCloseTo(0.05, 10);
  });
});
