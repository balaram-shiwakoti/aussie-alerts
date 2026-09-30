using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AussieAlerts.Data;
using AussieAlerts.DTOs;
using AussieAlerts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class IntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> UserAsync()
    {
        var client=factory.CreateClient();
        var response=await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest($"user-{Guid.NewGuid():N}@example.test", "TestPassword123!"));
        response.EnsureSuccessStatusCode();
        var auth=(await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",auth.AccessToken);
        return client;
    }
    private async Task<HttpClient> AdminAsync()
    {
        var client=factory.CreateClient();
        var response=await client.PostAsJsonAsync("/api/auth/login",new LoginRequest(ApiFactory.AdminEmail,ApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization=new("Bearer",(await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
        return client;
    }
    private async Task<int> MatchAsync()
    {
        await using var scope=factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MatchService>().RunAsync();
    }
    [Fact]
    public async Task End_to_end_matching_is_atomic_retry_safe_and_owner_scoped()
    {
        await factory.ResetAsync();
        using var user=await UserAsync(); using var other=await UserAsync(); using var admin=await AdminAsync();
        var request=new AlertRequest(" Darwin ","0800",400000,600000,2,"unit",true);
        // Old listing should not trigger a newly created alert.
        (await admin.PostAsJsonAsync("/api/admin/listings",new ListingRequest("Old fake unit","darwin","0800",500000,2,"unit"))).EnsureSuccessStatusCode();
        var created=await user.PostAsJsonAsync("/api/alerts",request);
        created.EnsureSuccessStatusCode(); var alert=(await created.Content.ReadFromJsonAsync<AlertDto>())!;
        Assert.Equal("darwin",alert.Suburb);
        Assert.Equal(HttpStatusCode.NotFound,(await other.PutAsJsonAsync($"/api/alerts/{alert.Id}",request)).StatusCode);
        foreach(var listing in new[] {
            new ListingRequest("Fake lower boundary","darwin","0800",400000,2,"unit"),
            new ListingRequest("Fake upper boundary","darwin","0800",600000,3,"unit"),
            new ListingRequest("Too expensive","darwin","0800",600001,2,"unit"),
            new ListingRequest("Wrong suburb","other","0800",500000,2,"unit"),
            new ListingRequest("Wrong postcode","darwin","0810",500000,2,"unit"),
            new ListingRequest("Wrong type","darwin","0800",500000,2,"house"),
            new ListingRequest("Too few beds","darwin","0800",500000,1,"unit")})
            (await admin.PostAsJsonAsync("/api/admin/listings",listing)).EnsureSuccessStatusCode();
        // Independent DbContexts simulate two replicas racing on the same data.
        await Task.WhenAll(MatchAsync(),MatchAsync());
        Assert.Equal(0,await MatchAsync());
        var inbox=(await user.GetFromJsonAsync<NotificationPage>("/api/notifications"))!;
        Assert.Equal(2,inbox.Total);
        Assert.Empty((await other.GetFromJsonAsync<NotificationPage>("/api/notifications"))!.Items);
        var id=inbox.Items[0].Id;
        Assert.Equal(HttpStatusCode.NotFound,(await other.PutAsync($"/api/notifications/{id}/read",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await user.PutAsync($"/api/notifications/{id}/read",null)).StatusCode);
        Assert.NotNull((await user.GetFromJsonAsync<NotificationPage>("/api/notifications"))!.Items.Single(x=>x.Id==id).ReadAt);
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(db.Database.HasPendingModelChanges());
        var sql=await db.Database.SqlQuery<string>($"""SELECT indexname AS "Value" FROM pg_indexes WHERE tablename = 'Listings'""").ToListAsync();
        Assert.Contains("IX_Listings_Postcode_Price",sql); Assert.Contains("IX_Listings_Price",sql);
    }
    [Fact]
    public async Task Authentication_validation_and_admin_boundary_are_enforced()
    {
        using var anon=factory.CreateClient(); using var user=await UserAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,(await anon.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await user.PostAsJsonAsync("/api/admin/listings",new ListingRequest("Fake","darwin","0800",1,2,"unit"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await user.PostAsJsonAsync("/api/alerts",new AlertRequest(null,"800",10,1,2,null,true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await user.GetAsync("/api/listings?Page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anon.PostAsJsonAsync("/api/auth/login",new LoginRequest(ApiFactory.AdminEmail,"wrong"))).StatusCode);
        var duplicate=new RegisterRequest($"dupe-{Guid.NewGuid():N}@example.test","TestPassword123!");
        (await anon.PostAsJsonAsync("/api/auth/register",duplicate)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,(await anon.PostAsJsonAsync("/api/auth/register",duplicate)).StatusCode);
        using var tampered=factory.CreateClient(); tampered.DefaultRequestHeaders.Authorization=new("Bearer","not-a-valid-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized,(await tampered.GetAsync("/api/alerts")).StatusCode);
    }
    [Fact]
    public async Task Pausing_and_editing_start_a_new_future_window()
    {
        await factory.ResetAsync(); using var user=await UserAsync(); using var admin=await AdminAsync();
        var request=new AlertRequest(null,"0800",0,1000000,0,null,false);
        var response=await user.PostAsJsonAsync("/api/alerts",request); response.EnsureSuccessStatusCode();
        var alert=(await response.Content.ReadFromJsonAsync<AlertDto>())!;
        (await admin.PostAsJsonAsync("/api/admin/listings",new ListingRequest("While paused","darwin","0800",500000,2,"unit"))).EnsureSuccessStatusCode();
        Assert.Equal(0,await MatchAsync());
        (await user.PutAsJsonAsync($"/api/alerts/{alert.Id}",request with { Active=true })).EnsureSuccessStatusCode();
        Assert.Equal(0,await MatchAsync());
        (await admin.PostAsJsonAsync("/api/admin/listings",new ListingRequest("After reactivation","darwin","0800",500000,2,"unit"))).EnsureSuccessStatusCode();
        Assert.Equal(1,await MatchAsync());
    }
}
