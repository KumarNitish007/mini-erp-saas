using System.Globalization;
using System.Text;

namespace MiniErp.Infrastructure.Pdf;

/// <summary>Indian-style money formatting: 12,34,567.89 and "Rupees Twelve Lakh ... Only".</summary>
public static class IndianNumberFormat
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    private static readonly string[] Ones =
    [
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    ];

    private static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    public static string Money(decimal amount) => amount.ToString("N2", India);

    public static string InWords(decimal amount)
    {
        if (amount < 0) return "Minus " + InWords(-amount);

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var rupees = (long)Math.Truncate(amount);
        var paise = (int)((amount - rupees) * 100);

        var sb = new StringBuilder("Rupees ");
        sb.Append(rupees == 0 ? "Zero" : Words(rupees));
        if (paise > 0) sb.Append(" and ").Append(Words(paise)).Append(" Paise");
        return sb.Append(" Only").ToString();
    }

    private static string Words(long n)
    {
        var parts = new List<string>();
        void Take(long divisor, string label)
        {
            if (n < divisor) return;
            parts.Add($"{Words(n / divisor)} {label}");
            n %= divisor;
        }

        Take(10_000_000, "Crore");
        Take(100_000, "Lakh");
        Take(1_000, "Thousand");
        Take(100, "Hundred");

        if (n > 0)
            parts.Add(n < 20 ? Ones[n] : Tens[n / 10] + (n % 10 > 0 ? " " + Ones[n % 10] : ""));

        return string.Join(" ", parts);
    }
}
