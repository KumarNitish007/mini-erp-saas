using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiniErp.Application.Common;
using MiniErp.Application.Features.Customers;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.Products;
using MiniErp.Domain.Tenants;
using MiniErp.Domain.Users;
using MiniErp.Infrastructure.Identity;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Creates a fictional demo company so the app has something to show on first run.
/// It goes through the real application services (validation, GST maths, numbering, stock),
/// so the demo data is produced by exactly the same rules as user data.
/// All names, GSTINs and logins here are made up and for local demo use only.
/// </summary>
public sealed class DemoDataSeeder(
    AppDbContext db,
    CurrentUser currentUser,
    IPasswordHasher hasher,
    ProductService products,
    CustomerService customers,
    InvoiceService invoices,
    TimeProvider clock,
    ILogger<DemoDataSeeder> logger)
{
    public const string DemoEmail = "demo@minierp.dev";
    public const string DemoPassword = "Demo@12345";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Tenants.AnyAsync(t => t.Slug == "demo-traders", ct)) return;

        var tenant = new Tenant("Demo Traders Pvt Ltd", "demo-traders", "27", "27AAPCD1234F1Z5",
            "Plot 12, MIDC Industrial Area, Pune 411019");
        var owner = new AppUser(tenant.Id, DemoEmail, "Demo Owner", UserRole.Owner);
        owner.SetPasswordHash(hasher.Hash(owner, DemoPassword));
        var staff = new AppUser(tenant.Id, "staff@minierp.dev", "Demo Staff", UserRole.Staff);
        staff.SetPasswordHash(hasher.Hash(staff, DemoPassword));

        db.Tenants.Add(tenant);
        db.Users.AddRange(owner, staff);
        await db.SaveChangesAsync(ct);

        currentUser.RunAs(tenant.Id, owner.Id, UserRole.Owner);

        var catalog = new List<ProductDto>();
        foreach (var p in new SaveProductRequest[]
        {
            new("PIPE-GI-25", "GI Pipe 25mm (6m length)", "pcs", "7306", 1450m, 18m, 40, 600),
            new("CEM-OPC-53", "OPC 53 Grade Cement (50kg)", "bag", "2523", 395m, 28m, 100, 1500),
            new("TMT-FE500-12", "TMT Bar Fe500 12mm", "kg", "7214", 62.5m, 18m, 500, 9000),
            new("CBL-CU-2.5", "Copper Wire 2.5 sq mm (90m)", "coil", "8544", 2350m, 18m, 25, 180),
            new("PNT-EMUL-20", "Exterior Emulsion Paint 20L", "can", "3209", 5200m, 18m, 10, 70),
            new("VLV-BR-15", "Brass Ball Valve 15mm", "pcs", "8481", 310m, 18m, 50, 450),
            new("BRK-RED-STD", "Red Clay Brick (standard)", "pcs", "6901", 9.5m, 5m, 5000, 40000),
            new("SAND-RIVER", "River Sand", "ton", "2505", 1850m, 5m, 20, 160),
            new("HLM-SAFE-ISI", "Safety Helmet ISI", "pcs", "6506", 240m, 18m, 60, 45),
            new("GLV-NIT-L", "Nitrile Work Gloves (L)", "pair", "4015", 85m, 12m, 200, 120)
        })
        {
            catalog.Add(await products.CreateAsync(p, ct));
        }

        var buyers = new List<CustomerDto>();
        foreach (var c in new SaveCustomerRequest[]
        {
            new("Sahyadri Builders LLP", "27", "accounts@sahyadri.example", "+91 00000 00001", "27AAKFS1234M1Z2", "Baner Road, Pune"),
            new("Konkan Infra Projects", "27", "billing@konkaninfra.example", "+91 00000 00002", null, "Ratnagiri, Maharashtra"),
            new("Deccan Structures Pvt Ltd", "36", "ap@deccan.example", "+91 00000 00003", "36AABCD5678K1Z9", "HITEC City, Hyderabad"),
            new("Narmada Constructions", "24", "finance@narmada.example", "+91 00000 00004", "24AAFCN4321P1Z4", "SG Highway, Ahmedabad"),
            new("Bengaluru Metro Contractors", "29", "pay@bmc.example", "+91 00000 00005", "29AAGCB8765Q1Z1", "Whitefield, Bengaluru"),
            new("Capital Interiors", "07", "hello@capitalinteriors.example", "+91 00000 00006", null, "Okhla Phase II, New Delhi")
        })
        {
            buyers.Add(await customers.CreateAsync(c, ct));
        }

        // ~6 months of invoices with a mix of paid, open, overdue and draft.
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var rng = new Random(42);
        var busy = catalog.Where(p => p.Sku is not ("HLM-SAFE-ISI" or "GLV-NIT-L")).ToList(); // keep two items low on stock
        var dates = Enumerable.Range(0, 28).Select(_ => today.AddDays(-rng.Next(0, 175))).Order().ToList();
        for (var n = 0; n < dates.Count; n++)
        {
            var date = dates[n];
            var customer = buyers[rng.Next(buyers.Count)];
            var lines = busy.OrderBy(_ => rng.Next()).Take(rng.Next(1, 4))
                .Select(p => new SaveInvoiceLineRequest(p.Id, Quantity(p, rng), null, null))
                .ToList();

            var draft = await invoices.CreateDraftAsync(new SaveInvoiceRequest(customer.Id, date, date.AddDays(30), null, lines), ct);
            if (n % 9 == 8) continue; // leave a few as drafts

            await invoices.IssueAsync(draft.Id, ct);
            if (date < today.AddDays(-75) || rng.NextDouble() < 0.3)
                await invoices.MarkPaidAsync(draft.Id, ct);
        }

        logger.LogInformation("Seeded demo tenant '{Tenant}'. Sign in with {Email}.", tenant.Name, DemoEmail);
    }

    private static decimal Quantity(ProductDto p, Random rng) => p.Unit switch
    {
        "kg" => rng.Next(2, 20) * 50,
        "pcs" when p.UnitPrice < 20 => rng.Next(5, 30) * 100,
        "bag" => rng.Next(2, 12) * 10,
        _ => rng.Next(1, 8)
    };
}
