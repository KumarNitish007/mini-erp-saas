using System.Text.RegularExpressions;

namespace MiniErp.Application.Common;

public sealed record StateDto(string Code, string Name);

/// <summary>GST state codes, used for place-of-supply (CGST/SGST vs IGST) and GSTIN validation.</summary>
public static partial class IndianStates
{
    public static readonly IReadOnlyList<StateDto> All =
    [
        new("01", "Jammu & Kashmir"), new("02", "Himachal Pradesh"), new("03", "Punjab"), new("04", "Chandigarh"),
        new("05", "Uttarakhand"), new("06", "Haryana"), new("07", "Delhi"), new("08", "Rajasthan"),
        new("09", "Uttar Pradesh"), new("10", "Bihar"), new("11", "Sikkim"), new("12", "Arunachal Pradesh"),
        new("13", "Nagaland"), new("14", "Manipur"), new("15", "Mizoram"), new("16", "Tripura"),
        new("17", "Meghalaya"), new("18", "Assam"), new("19", "West Bengal"), new("20", "Jharkhand"),
        new("21", "Odisha"), new("22", "Chhattisgarh"), new("23", "Madhya Pradesh"), new("24", "Gujarat"),
        new("26", "Dadra & Nagar Haveli and Daman & Diu"), new("27", "Maharashtra"), new("29", "Karnataka"),
        new("30", "Goa"), new("31", "Lakshadweep"), new("32", "Kerala"), new("33", "Tamil Nadu"),
        new("34", "Puducherry"), new("35", "Andaman & Nicobar Islands"), new("36", "Telangana"),
        new("37", "Andhra Pradesh"), new("38", "Ladakh"), new("97", "Other Territory")
    ];

    private static readonly HashSet<string> Codes = All.Select(s => s.Code).ToHashSet();

    public static bool IsValidCode(string? code) => code is not null && Codes.Contains(code);

    public static string NameOf(string code) => All.FirstOrDefault(s => s.Code == code)?.Name ?? code;

    /// <summary>Format check for a 15-character GSTIN whose first two digits are the state code.</summary>
    public static bool IsValidGstin(string? gstin, string? stateCode) =>
        gstin is not null && GstinPattern().IsMatch(gstin) && (stateCode is null || gstin[..2] == stateCode);

    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")]
    private static partial Regex GstinPattern();
}
