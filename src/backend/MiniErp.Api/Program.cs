using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MiniErp.Api.Infrastructure;
using MiniErp.Application;
using MiniErp.Infrastructure;
using MiniErp.Infrastructure.Identity;
using MiniErp.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---- Secrets: never in source control -------------------------------------------------
// Jwt:Key and ConnectionStrings:Default come from environment variables / user-secrets.
// For a zero-config local run only, Development falls back to a random per-process key.
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]) && builder.Environment.IsDevelopment())
    builder.Configuration["Jwt:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SigningKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };
    });

// Secure by default: every endpoint needs a valid token unless marked [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Manager, p => p.RequireRole("Owner", "Admin"))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(Policies.AuthRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimit:AuthPerMinute", 10),
            Window = TimeSpan.FromMinutes(1)
        }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("Default")!, name: "sql");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MiniERP API", Version = "v1", Description = "Multi-tenant inventory & GST invoicing" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
});

var app = builder.Build();

// ---- Database: apply /database scripts, then optional demo data ------------------------
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    DatabaseMigrator.Migrate(app.Configuration.GetConnectionString("Default")!,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrator"),
        createDatabaseIfMissing: app.Configuration.GetValue("Database:CreateIfMissing", false));
}

if (app.Configuration.GetValue("Database:SeedDemoData", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.DocumentTitle = "MiniERP API");
}

// Single-host deployments (e.g. Azure App Service) ship the Angular build in wwwroot.
// Static files are served before auth so the fallback "authenticated" policy doesn't apply to them.
var hostsSpa = Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot"));
if (hostsSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

if (hostsSpa) app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
