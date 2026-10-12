import { InrPipe, todayIso } from './ui';

describe('InrPipe', () => {
  const pipe = new InrPipe();

  it('formats rupees with Indian digit grouping', () => {
    expect(pipe.transform(1234567.5)).toBe('₹12,34,567.50');
  });

  it('shows a dash for missing values', () => {
    expect(pipe.transform(null)).toBe('—');
  });
});

describe('todayIso', () => {
  it('returns an ISO date that <input type="date"> accepts', () => {
    expect(todayIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});
