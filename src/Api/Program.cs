using System.Text;
using System.Threading.RateLimiting;
using AussieAlerts.Data;
using AussieAlerts.Domain;
using AussieAlerts.DTOs;
using AussieAlerts.Infrastructure;
using AussieAlerts.Repositories;
using AussieAlerts.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var migrateOnly = args.Contains("--migrate-only");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate-only").ToArray());
builder.Host.UseSerilog((_, log) => log.MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext().WriteTo.Console());
var config = builder.Configuration;
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<AlertValidator>();
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(config.GetConnectionString("Database")));
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AlertRepository>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddHostedService<MatchingWorker>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    var jwtKey = config["Jwt:Key"]!;
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = config["Jwt:Audience"],
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(10),
        RoleClaimType = "role", NameClaimType = "sub", ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(config["Frontend:Origin"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddHealthChecks().AddDbContextCheck<AppDb>("postgres", tags: ["ready"]);
var app = builder.Build();
// Validate after Build so WebApplicationFactory configuration overrides are available too.
var configuredJwtKey = config["Jwt:Key"];
if (string.IsNullOrWhiteSpace(configuredJwtKey) || Encoding.UTF8.GetByteCount(configuredJwtKey) < 32)
    throw new InvalidOperationException("Set Jwt__Key to a random secret of at least 32 bytes.");
if (config.GetValue("Matching:IntervalSeconds", 15) < 1)
    throw new InvalidOperationException("Matching interval must be positive.");

// Local convenience or explicitly requested one-shot operation. Production migration is a separate deployment step.
if (migrateOnly || config.GetValue("Database:AutoMigrate", false) || config.GetValue("Seed:Enabled", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    if (migrateOnly || config.GetValue("Database:AutoMigrate", false))
        await scope.ServiceProvider.GetRequiredService<AppDb>().Database.MigrateAsync();
    if (config.GetValue("Seed:Enabled", false)) await AdminBootstrap.RunAsync(scope.ServiceProvider, config);
}
if (migrateOnly) return;
app.UseExceptionHandler();
app.UseSerilogRequestLogging(); // Never enable request-body or Authorization-header logging.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Aussie Alerts v1"));
}
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready") });
app.MapControllers();
app.MapFallback(async context =>
{
    var path = context.Request.Path;
    var index = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "index.html");
    if (path.StartsWithSegments("/api") || path.StartsWithSegments("/health") || !File.Exists(index))
    { context.Response.StatusCode = 404; return; }
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(index);
});
await app.RunAsync();
public partial class Program { }
