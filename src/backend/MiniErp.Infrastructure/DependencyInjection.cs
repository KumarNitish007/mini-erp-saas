using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Common;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Pdf;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Reporting;

namespace MiniErp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured. Set ConnectionStrings__Default.");

        services.AddDbContext<AppDbContext>(options => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
            // Lines/customers are always reached through their (already tenant-filtered) invoice.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddSingleton<TimeProvider>(new BusinessTimeProvider(configuration["App:TimeZone"] ?? "Asia/Kolkata"));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .Validate(o => o.Key.Length >= 32, "Jwt:Key must be at least 32 characters.")
            .ValidateOnStart();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();

        services.AddSingleton(new ConnectionStrings(connectionString));
        services.AddScoped<IInvoiceNumberGenerator, SqlInvoiceNumberGenerator>();
        services.AddScoped<IReportQueries, SqlReportQueries>();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddSingleton<IInvoicePdfGenerator, QuestPdfInvoiceGenerator>();

        services.AddScoped<DemoDataSeeder>();
        return services;
    }
}
