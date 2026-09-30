using AussieAlerts.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
namespace Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public const string AdminEmail = "admin@example.test";
    public const string AdminPassword = "TestAdminPassword123!";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["ConnectionStrings:Database"] = postgres.GetConnectionString(),
            ["Jwt:Key"] = "integration-test-signing-key-32-bytes-minimum-only",
            ["Jwt:Issuer"] = "aussie-alerts", ["Jwt:Audience"] = "aussie-alerts-web",
            ["Matching:Enabled"] = "false", ["Database:AutoMigrate"] = "true",
            ["Seed:Enabled"] = "true", ["Seed:AdminEmail"] = AdminEmail, ["Seed:AdminPassword"] = AdminPassword,
        }));
    }
    public async Task InitializeAsync() { await postgres.StartAsync(); _ = CreateClient(); }
    async Task IAsyncLifetime.DisposeAsync() { await base.DisposeAsync(); await postgres.DisposeAsync(); }
    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Database.ExecuteSqlRawAsync("""TRUNCATE TABLE "Notifications", "Alerts", "Listings" CASCADE""");
    }
}
