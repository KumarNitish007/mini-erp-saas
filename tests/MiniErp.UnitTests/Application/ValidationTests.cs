using MiniErp.Application.Common;
using MiniErp.Application.Features.Auth;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.Products;
using Shouldly;

namespace MiniErp.UnitTests.Application;

public class ValidationTests
{
    [Theory]
    [InlineData("27AAPCD1234F1Z5", "27", true)]
    [InlineData("27AAPCD1234F1Z5", "29", false)] // state mismatch
    [InlineData("27aapcd1234f1z5", "27", false)] // must be upper case
    [InlineData("27AAPCD1234F1X5", "27", false)] // 14th char must be Z
    [InlineData("27AAPCD1234F1Z", "27", false)]  // too short
    public void Gstin_format_and_state_are_checked(string gstin, string state, bool expected)
    {
        IndianStates.IsValidGstin(gstin, state).ShouldBe(expected);
    }

    [Fact]
    public void Registration_requires_a_strong_password_and_valid_state()
    {
        var result = new RegisterTenantValidator().Validate(
            new RegisterTenantRequest("Acme", "99", null, null, "Owner", "owner@acme.test", "weakpass"));

        result.Errors.Select(e => e.PropertyName).ShouldBe(["StateCode", "Password", "Password"], ignoreOrder: true);
    }

    [Fact]
    public void Product_rejects_unknown_gst_slab_and_bad_hsn()
    {
        var result = new SaveProductValidator().Validate(
            new SaveProductRequest("SKU-1", "Thing", "pcs", "12", 10m, 7m, 0));

        result.Errors.Select(e => e.PropertyName).ShouldBe(["HsnCode", "GstRate"], ignoreOrder: true);
    }

    [Fact]
    public void Invoice_needs_lines_with_positive_quantities()
    {
        var today = new DateOnly(2026, 10, 1);
        var validator = new SaveInvoiceValidator();

        validator.Validate(new SaveInvoiceRequest(Guid.NewGuid(), today, today, null, [])).IsValid.ShouldBeFalse();
        validator.Validate(new SaveInvoiceRequest(Guid.NewGuid(), today, today, null,
            [new SaveInvoiceLineRequest(Guid.NewGuid(), 0, null, null)])).IsValid.ShouldBeFalse();
        validator.Validate(new SaveInvoiceRequest(Guid.NewGuid(), today, today.AddDays(-1), null,
            [new SaveInvoiceLineRequest(Guid.NewGuid(), 1, null, null)])).IsValid.ShouldBeFalse();
        validator.Validate(new SaveInvoiceRequest(Guid.NewGuid(), today, today.AddDays(15), null,
            [new SaveInvoiceLineRequest(Guid.NewGuid(), 1, null, null)])).IsValid.ShouldBeTrue();
    }
}
