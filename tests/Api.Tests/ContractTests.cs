using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api.Tests;

public sealed class ContractTests
{
    [Fact]
    public async Task Api_boots_without_database_for_contract_export_and_protects_routes()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "contract-test-key-at-least-thirty-two-bytes-long",
                ["ConnectionStrings:Database"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Database:AutoMigrate"] = "false",
                ["Seed:Enabled"] = "false",
                ["Matching:Enabled"] = "false"
            }));
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var doc = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        Assert.NotNull(doc["paths"]?["/api/admin/listings"]);
        Assert.NotNull(doc["components"]?["schemas"]?["AlertRequest"]);
    }
}
