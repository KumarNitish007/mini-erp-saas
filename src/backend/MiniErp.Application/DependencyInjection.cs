using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Features.Auth;
using MiniErp.Application.Features.Customers;
using MiniErp.Application.Features.Dashboard;
using MiniErp.Application.Features.Invoices;
using MiniErp.Application.Features.Products;
using MiniErp.Application.Features.Users;

namespace MiniErp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ProductService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
