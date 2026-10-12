using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using MiniErp.Application.Features.Auth;
using Testcontainers.MsSql;

namespace MiniErp.IntegrationTests;

/// <summary>
/// Boots the real API against a real SQL Server, so the DbUp scripts, stored procedures,
/// EF mappings and tenant filters are all exercised.
///  - CI / Docker available: a throwaway SQL Server container (Testcontainers).
///  - Otherwise: set MINIERP_TEST_SQL to any SQL Server you own (e.g. LocalDB);
///    a uniquely named database is created and dropped afterwards.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private MsSqlContainer? _container;
    private string _connectionString = "";
    private bool _dropDatabaseOnDispose;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("MINIERP_TEST_SQL");
        if (!string.IsNullOrWhiteSpace(external))
        {
            _connectionString = new SqlConnectionStringBuilder(external) { InitialCatalog = $"MiniErp_Test_{Guid.NewGuid():N}" }.ConnectionString;
            _dropDatabaseOnDispose = true;
        }
        else
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            _connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "MiniErp_Test" }.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:CreateIfMissing", "true");
        builder.UseSetting("Database:SeedDemoData", "false");
        builder.UseSetting("RateLimit:AuthPerMinute", "10000");
    }

    /// <summary>Signs up a brand-new tenant and returns a client authenticated as its owner.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> NewTenantAsync(string stateCode = "27")
    {
        var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterTenantRequest(
            $"Test Co {suffix}", stateCode, null, null, "Test Owner", $"owner-{suffix}@test.local", "Passw0rd!"));
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        if (_dropDatabaseOnDispose)
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var database = builder.InitialCatalog;
            builder.InitialCatalog = "master";
            SqlConnection.ClearAllPools();
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                $"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        if (_container is not null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
