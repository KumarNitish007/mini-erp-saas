using MiniErp.Infrastructure.Pdf;
using Shouldly;

namespace MiniErp.UnitTests.Infrastructure;

public class IndianNumberFormatTests
{
    [Theory]
    [InlineData(1234567.891, "12,34,567.89")]
    [InlineData(999, "999.00")]
    [InlineData(100000, "1,00,000.00")]
    public void Money_uses_lakh_crore_grouping(decimal value, string expected)
    {
        IndianNumberFormat.Money(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, "Rupees Zero Only")]
    [InlineData(15, "Rupees Fifteen Only")]
    [InlineData(1942.50, "Rupees One Thousand Nine Hundred Forty Two and Fifty Paise Only")]
    [InlineData(100000, "Rupees One Lakh Only")]
    [InlineData(12345678.05, "Rupees One Crore Twenty Three Lakh Forty Five Thousand Six Hundred Seventy Eight and Five Paise Only")]
    public void Amount_in_words_uses_indian_system(decimal value, string expected)
    {
        IndianNumberFormat.InWords(value).ShouldBe(expected);
    }
}
