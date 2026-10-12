using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MiniErp.Application.Common;
using MiniErp.Application.Features.Auth;
using MiniErp.Application.Features.Dashboard;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.Products;
using MiniErp.Application.Features.Users;
using Shouldly;

namespace MiniErp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Registered_owner_can_read_their_profile()
    {
        var (client, auth) = await api.NewTenantAsync();

        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/auth/me");

        me!.Role.ShouldBe("Owner");
        me.TenantId.ShouldBe(auth.User.TenantId);
    }

    [Fact]
    public async Task Api_requires_a_token()
    {
        var response = await api.CreateClient().GetAsync("/api/products");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var (_, auth) = await api.NewTenantAsync();

        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(auth.User.Email, "Wrong-pass1"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Duplicate_email_cannot_register_twice()
    {
        var (_, auth) = await api.NewTenantAsync();

        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterTenantRequest("Another Co", "27", null, null, "X", auth.User.Email, "Passw0rd!"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}

[Collection(ApiCollection.Name)]
public class TenantIsolationTests(ApiFactory api)
{
    [Fact]
    public async Task One_tenant_can_never_see_another_tenants_data()
    {
        var (alpha, _) = await api.NewTenantAsync();
        var (beta, _) = await api.NewTenantAsync();

        var product = await alpha.CreateProductAsync();
        var customer = await alpha.CreateCustomerAsync();
        var invoice = await alpha.CreateDraftAsync(customer.Id, (product.Id, 1));

        (await beta.GetAsync($"/api/products/{product.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await beta.GetAsync($"/api/customers/{customer.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await beta.GetAsync($"/api/invoices/{invoice.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await beta.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products"))!.TotalCount.ShouldBe(0);

        // ...and cannot reference it either.
        var betaCustomer = await beta.CreateCustomerAsync();
        var response = await beta.PostAsJsonAsync("/api/invoices", new SaveInvoiceRequest(betaCustomer.Id, TestData.Today,
            TestData.Today, null, [new SaveInvoiceLineRequest(product.Id, 1, null, null)]));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

[Collection(ApiCollection.Name)]
public class InvoiceFlowTests(ApiFactory api)
{
    [Fact]
    public async Task Issuing_numbers_invoices_sequentially_and_deducts_stock()
    {
        var (client, _) = await api.NewTenantAsync();
        var product = await client.CreateProductAsync(stock: 10);
        var customer = await client.CreateCustomerAsync();

        var first = await Issue(client, (await client.CreateDraftAsync(customer.Id, (product.Id, 3))).Id);
        var second = await Issue(client, (await client.CreateDraftAsync(customer.Id, (product.Id, 2))).Id);

        first.Number.ShouldBe($"INV-{TestData.Today.Year}-00001");
        second.Number.ShouldBe($"INV-{TestData.Today.Year}-00002");
        (await client.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}"))!.StockQuantity.ShouldBe(5);

        var movements = await client.GetFromJsonAsync<List<StockMovementDto>>($"/api/products/{product.Id}/movements");
        movements!.Select(m => m.Type).ShouldBe(["Sale", "Sale", "Opening"]);
    }

    [Fact]
    public async Task Failed_issue_rolls_back_stock_and_does_not_burn_a_number()
    {
        var (client, _) = await api.NewTenantAsync();
        var product = await client.CreateProductAsync(stock: 2);
        var customer = await client.CreateCustomerAsync();

        var tooBig = await client.CreateDraftAsync(customer.Id, (product.Id, 5));
        var response = await client.PostAsync($"/api/invoices/{tooBig.Id}/issue", null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}"))!.StockQuantity.ShouldBe(2);

        var ok = await Issue(client, (await client.CreateDraftAsync(customer.Id, (product.Id, 1))).Id);
        ok.Number.ShouldBe($"INV-{TestData.Today.Year}-00001");
    }

    [Theory]
    [InlineData("27", false)] // same state as seller → CGST + SGST
    [InlineData("29", true)]  // different state  → IGST
    public async Task Place_of_supply_decides_the_tax_split(string customerState, bool expectIgst)
    {
        var (client, _) = await api.NewTenantAsync(stateCode: "27");
        var product = await client.CreateProductAsync(price: 1000m, gst: 18m);
        var customer = await client.CreateCustomerAsync(customerState);

        var draft = await client.CreateDraftAsync(customer.Id, (product.Id, 2));

        draft.IsInterState.ShouldBe(expectIgst);
        draft.SubTotal.ShouldBe(2000m);
        draft.GrandTotal.ShouldBe(2360m);
        if (expectIgst) draft.IgstAmount.ShouldBe(360m);
        else { draft.CgstAmount.ShouldBe(180m); draft.SgstAmount.ShouldBe(180m); }
    }

    [Fact]
    public async Task Cancelling_an_issued_invoice_restores_stock()
    {
        var (client, _) = await api.NewTenantAsync();
        var product = await client.CreateProductAsync(stock: 10);
        var customer = await client.CreateCustomerAsync();
        var invoice = await Issue(client, (await client.CreateDraftAsync(customer.Id, (product.Id, 4))).Id);

        var cancelled = await (await client.PostAsync($"/api/invoices/{invoice.Id}/cancel", null)).Content.ReadFromJsonAsync<InvoiceDetailDto>();

        cancelled!.Status.ShouldBe("Cancelled");
        (await client.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}"))!.StockQuantity.ShouldBe(10);
    }

    [Fact]
    public async Task Staff_cannot_cancel_invoices()
    {
        var (owner, _) = await api.NewTenantAsync();
        var email = $"staff-{Guid.NewGuid():N}"[..20] + "@test.local";
        (await owner.PostAsJsonAsync("/api/users", new CreateUserRequest(email, "Staff Member", "Staff", "Passw0rd!"))).EnsureSuccessStatusCode();

        var login = await (await api.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Passw0rd!")))
            .Content.ReadFromJsonAsync<AuthResponse>();
        var staff = api.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var product = await owner.CreateProductAsync();
        var customer = await owner.CreateCustomerAsync();
        var invoice = await Issue(owner, (await owner.CreateDraftAsync(customer.Id, (product.Id, 1))).Id);

        (await staff.PostAsync($"/api/invoices/{invoice.Id}/cancel", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await staff.GetAsync("/api/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await staff.GetAsync($"/api/invoices/{invoice.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Invoice_pdf_is_downloadable()
    {
        var (client, _) = await api.NewTenantAsync();
        var product = await client.CreateProductAsync();
        var customer = await client.CreateCustomerAsync();
        var invoice = await Issue(client, (await client.CreateDraftAsync(customer.Id, (product.Id, 1))).Id);

        var response = await client.GetAsync($"/api/invoices/{invoice.Id}/pdf");

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await response.Content.ReadAsByteArrayAsync()).Take(4).ShouldBe("%PDF"u8.ToArray());
    }

    [Fact]
    public async Task Dashboard_stored_procedures_report_issued_sales()
    {
        var (client, _) = await api.NewTenantAsync();
        var product = await client.CreateProductAsync(stock: 3, price: 1000m, gst: 18m); // reorder level 5 → low stock
        var customer = await client.CreateCustomerAsync();
        await Issue(client, (await client.CreateDraftAsync(customer.Id, (product.Id, 1))).Id);
        await client.CreateDraftAsync(customer.Id, (product.Id, 1));

        var dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/dashboard?months=3");

        dashboard!.Kpis.OutstandingAmount.ShouldBe(1180m);
        dashboard.Kpis.RevenueThisMonth.ShouldBe(1180m);
        dashboard.Kpis.DraftCount.ShouldBe(1);
        dashboard.Kpis.LowStockCount.ShouldBe(1);
        dashboard.MonthlySales.Count.ShouldBe(3);
        dashboard.MonthlySales[^1].GrandTotal.ShouldBe(1180m);
        dashboard.TopCustomers.ShouldHaveSingleItem().Name.ShouldBe(customer.Name);
    }

    private static async Task<InvoiceDetailDto> Issue(HttpClient client, Guid id)
    {
        var response = await client.PostAsync($"/api/invoices/{id}/issue", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceDetailDto>())!;
    }
}
