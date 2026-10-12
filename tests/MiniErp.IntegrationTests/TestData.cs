using System.Net.Http.Json;
using MiniErp.Application.Features.Customers;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.Products;

namespace MiniErp.IntegrationTests;

internal static class TestData
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    public static async Task<ProductDto> CreateProductAsync(this HttpClient client, decimal stock = 100, decimal price = 1000m, decimal gst = 18m)
    {
        var sku = "SKU-" + Guid.NewGuid().ToString("N")[..6];
        var response = await client.PostAsJsonAsync("/api/products",
            new SaveProductRequest(sku, $"Product {sku}", "pcs", "7306", price, gst, 5, stock));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    public static async Task<CustomerDto> CreateCustomerAsync(this HttpClient client, string stateCode = "27")
    {
        var response = await client.PostAsJsonAsync("/api/customers",
            new SaveCustomerRequest("Customer " + Guid.NewGuid().ToString("N")[..6], stateCode, null, null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    public static async Task<InvoiceDetailDto> CreateDraftAsync(this HttpClient client, Guid customerId, params (Guid ProductId, decimal Qty)[] lines)
    {
        var response = await client.PostAsJsonAsync("/api/invoices", new SaveInvoiceRequest(
            customerId, Today, Today.AddDays(15), null,
            lines.Select(l => new SaveInvoiceLineRequest(l.ProductId, l.Qty, null, null)).ToList()));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceDetailDto>())!;
    }
}
