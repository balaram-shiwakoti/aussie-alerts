# Complete solution source

All authored code/configuration, the generated EF migration and the generated TypeScript schema are included below. Runnable files and the npm lockfile / OpenAPI JSON are in the ZIP. Copy a file as a whole; do not paste all files into one source file.

## `.dockerignore`

```text
.git
.env
.env.*
**/node_modules
**/bin
**/obj
**/dist
**/coverage
**/TestResults
```

## `.editorconfig`

```text
root = true
[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
[*.cs]
indent_style = space
indent_size = 4
```

## `.env.example`

```text
POSTGRES_DB=alerts
POSTGRES_USER=alerts
POSTGRES_PASSWORD=GENERATE_WITH_SETUP_SCRIPT
JWT_KEY=GENERATE_WITH_SETUP_SCRIPT
ADMIN_EMAIL=admin@example.test
ADMIN_PASSWORD=GENERATE_WITH_SETUP_SCRIPT
```

## `.github/workflows/ci.yml`

```yaml
name: Build test lint
on:
  push:
  pull_request:
permissions:
  contents: read
jobs:
  verify:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - uses: actions/setup-node@v4
        with:
          node-version: '24'
          cache: npm
          cache-dependency-path: web/package-lock.json
      - run: dotnet tool restore
      - run: dotnet restore AussieAlerts.slnx -m:1
      - run: dotnet build AussieAlerts.slnx -m:1 --no-restore -c Release
      - name: C# formatting
        run: dotnet format whitespace . --folder --include src tests --exclude src/Api/obj src/Api/bin tests/Api.Tests/obj tests/Api.Tests/bin --verify-no-changes
      - name: xUnit and real PostgreSQL Testcontainers tests
        run: dotnet test tests/Api.Tests --no-build -c Release --logger trx
      - run: dotnet ef migrations has-pending-model-changes --project src/Api
      - run: npm ci --prefix web
      - name: Check generated API contract
        shell: bash
        run: |
          node scripts/setup.mjs
          node scripts/run-api.mjs --contract > api-contract.log 2>&1 &
          API_PID=$!
          trap 'kill "$API_PID" || true' EXIT
          for i in {1..60}; do
            if curl --fail --silent http://localhost:8080/health/live; then break; fi
            sleep 1
          done
          node scripts/export-openapi.mjs
          npm --prefix web run generate:api
          git diff --exit-code -- openapi.json web/src/api/schema.d.ts
      - run: npm --prefix web run lint
      - run: npm --prefix web test
      - run: npm --prefix web run build
      - name: Build production container
        run: docker build --target production -t aussie-alerts:ci .
```

## `.gitignore`

```text
.env
.env.*
!.env.example
**/bin/
**/obj/
**/node_modules/
**/dist/
**/coverage/
**/TestResults/
.vs/
*.user
*.log
*.tsbuildinfo
```

## `AussieAlerts.slnx`

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Api/Api.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Api.Tests/Api.Tests.csproj" />
  </Folder>
</Solution>
```

## `Directory.Build.props`

```xml
<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>
```

## `Dockerfile`

```text
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /repo
COPY Directory.Build.props ./
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj
COPY src/Api src/Api
RUN dotnet publish src/Api/Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api-runtime
WORKDIR /app
COPY --from=api-build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]

FROM node:24-alpine AS web-build
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ .
RUN npm run build

# Azure/production: ASP.NET serves the React build and API on one HTTPS origin.
FROM api-runtime AS production
COPY --from=web-build /web/dist ./wwwroot
```

## `compose.yaml`

```yaml
services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: ${POSTGRES_DB:?Run node scripts/setup.mjs}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    ports: ["127.0.0.1:5432:5432"]
    volumes: ["postgres-data:/var/lib/postgresql/data"]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $$POSTGRES_USER -d $$POSTGRES_DB"]
      interval: 5s
      timeout: 5s
      retries: 20
  api:
    build:
      context: .
      target: api-runtime
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Database: Host=postgres;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      Jwt__Key: ${JWT_KEY:?Run node scripts/setup.mjs}
      Database__AutoMigrate: "true"
      Seed__Enabled: "true"
      Seed__AdminEmail: ${ADMIN_EMAIL}
      Seed__AdminPassword: ${ADMIN_PASSWORD}
      Frontend__Origin: http://localhost:5173
    ports: ["127.0.0.1:8080:8080"]
    depends_on:
      postgres:
        condition: service_healthy
  web:
    build:
      context: web
      dockerfile: Dockerfile.dev
    environment:
      API_PROXY_TARGET: http://api:8080
    ports: ["127.0.0.1:5173:5173"]
    volumes:
      - ./web:/app
      - web-modules:/app/node_modules
    depends_on: [api]
volumes:
  postgres-data:
  web-modules:
```

## `dotnet-tools.json`

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "10.0.12",
      "commands": [
        "dotnet-ef"
      ],
      "rollForward": false
    }
  }
}
```

## `scripts/export-openapi.mjs`

```javascript
import { writeFileSync } from 'node:fs';
const response=await fetch('http://localhost:8080/openapi/v1.json');
if(!response.ok) throw new Error(`OpenAPI export failed: ${response.status}`);
writeFileSync('openapi.json', JSON.stringify(await response.json(),null,2)+'\n');
console.log('Exported openapi.json. Run npm --prefix web run generate:api next.');
```

## `scripts/run-api.mjs`

```javascript
import { readFileSync } from 'node:fs';
import { spawn } from 'node:child_process';
const values = Object.fromEntries(readFileSync('.env','utf8').split(/\r?\n/).filter(x=>x && !x.startsWith('#')).map(line=>{
  const i=line.indexOf('='); return [line.slice(0,i),line.slice(i+1)];
}));
const exportOnly = process.argv.includes('--contract');
const child = spawn('dotnet',['run','--project','src/Api','--no-launch-profile', ...(process.argv.includes('--migrate-only') ? ['--','--migrate-only'] : [])], {
  stdio:'inherit', env:{...process.env,
    ASPNETCORE_ENVIRONMENT:'Development', ASPNETCORE_URLS:'http://localhost:8080',
    ConnectionStrings__Database:`Host=localhost;Database=${values.POSTGRES_DB};Username=${values.POSTGRES_USER};Password=${values.POSTGRES_PASSWORD}`,
    Jwt__Key:values.JWT_KEY,
    Database__AutoMigrate:exportOnly ? 'false' : 'true', Seed__Enabled:exportOnly ? 'false' : 'true',
    Seed__AdminEmail:values.ADMIN_EMAIL, Seed__AdminPassword:values.ADMIN_PASSWORD,
    Matching__Enabled:exportOnly ? 'false' : 'true',
  },
});
child.on('error',e=>{console.error(e.message);process.exit(1);});
for (const signal of ['SIGINT','SIGTERM']) process.on(signal,()=>child.kill(signal));
child.on('exit',code=>process.exit(code ?? 1));
```

## `scripts/setup.mjs`

```javascript
import { randomBytes } from 'node:crypto';
import { existsSync, writeFileSync } from 'node:fs';
if (existsSync('.env')) { console.log('.env already exists; keeping your settings.'); process.exit(0); }
const env = `POSTGRES_DB=alerts
POSTGRES_USER=alerts
POSTGRES_PASSWORD=${randomBytes(24).toString('hex')}
JWT_KEY=${randomBytes(48).toString('base64')}
ADMIN_EMAIL=admin@example.test
ADMIN_PASSWORD=DemoA1!${randomBytes(18).toString('hex')}
`;
writeFileSync('.env', env, { mode: 0o600 });
console.log('Created .env. Open it locally to find the demo admin password. Never commit this file.');
```

## `src/Api/Api.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup><RootNamespace>AussieAlerts</RootNamespace><UserSecretsId>aussie-alerts-local</UserSecretsId></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.12" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12"><PrivateAssets>all</PrivateAssets></PackageReference>
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.12" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.0.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" Version="7.2.0" />
  </ItemGroup>
</Project>
```

## `src/Api/Controllers/AlertsController.cs`

```csharp
using System.Security.Claims;
using AussieAlerts.DTOs;
using AussieAlerts.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AussieAlerts.Controllers;

[ApiController, Route("api/alerts"), Authorize]
public sealed class AlertsController(AlertService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet]
    public Task<List<AlertDto>> List(CancellationToken ct) => service.ListAsync(Owner, ct);
    [HttpPost]
    public async Task<ActionResult<AlertDto>> Create(AlertRequest request, IValidator<AlertRequest> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Ok(await service.CreateAsync(Owner, request, ct));
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AlertDto>> Update(Guid id, AlertRequest request, IValidator<AlertRequest> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Ok(await service.UpdateAsync(Owner, id, request, ct));
    }
}
```

## `src/Api/Controllers/AuthController.cs`

```csharp
using AussieAlerts.DTOs;
using AussieAlerts.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace AussieAlerts.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(AuthService service) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request,
        IValidator<RegisterRequest> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Ok(await service.RegisterAsync(request, ct));
    }
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request,
        IValidator<LoginRequest> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Ok(await service.LoginAsync(request, ct));
    }
}
```

## `src/Api/Controllers/ListingsController.cs`

```csharp
using AussieAlerts.Data;
using AussieAlerts.Domain;
using AussieAlerts.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Controllers;

[ApiController, Route("api/listings"), Authorize]
public sealed class ListingsController(AppDb db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ListingPage>> Browse([FromQuery] ListingQuery request,
        IValidator<ListingQuery> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var query = db.Listings.AsNoTracking().AsQueryable();
        if (request.Postcode != null) query = query.Where(x => x.Postcode == request.Postcode);
        if (request.MinPrice != null) query = query.Where(x => x.Price >= request.MinPrice);
        if (request.MaxPrice != null) query = query.Where(x => x.Price <= request.MaxPrice);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new ListingDto(x.Id, x.Title, x.Suburb, x.Postcode, x.Price,
                x.Bedrooms, x.PropertyType, x.CreatedAt)).ToListAsync(ct);
        return Ok(new ListingPage(items, total, request.Page, request.PageSize));
    }
}

[ApiController, Route("api/admin/listings"), Authorize(Roles = "Admin")]
public sealed class AdminController(AppDb db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ListingDto>> Create(ListingRequest request, IValidator<ListingRequest> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var listing = new Listing { Title = request.Title.Trim(), Suburb = InputRules.Normalize(request.Suburb)!,
            Postcode = request.Postcode, Price = request.Price, Bedrooms = request.Bedrooms,
            PropertyType = request.PropertyType };
        db.Listings.Add(listing);
        await db.SaveChangesAsync(ct);
        return Ok(new ListingDto(listing.Id, listing.Title, listing.Suburb, listing.Postcode,
            listing.Price, listing.Bedrooms, listing.PropertyType, listing.CreatedAt));
    }
}
```

## `src/Api/Controllers/NotificationsController.cs`

```csharp
using System.Security.Claims;
using AussieAlerts.Data;
using AussieAlerts.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Controllers;

[ApiController, Route("api/notifications"), Authorize]
public sealed class NotificationsController(AppDb db) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet]
    public async Task<ActionResult<NotificationPage>> List([FromQuery] PageQuery request,
        IValidator<PageQuery> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var query = db.Notifications.AsNoTracking().Where(x => x.UserId == Owner);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new NotificationDto(x.Id, x.AlertId, x.ListingId, x.Subject, x.Body, x.CreatedAt, x.ReadAt)).ToListAsync(ct);
        return Ok(new NotificationPage(items, total, request.Page, request.PageSize));
    }
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var count = await db.Notifications.Where(x => x.Id == id && x.UserId == Owner)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, x => x.ReadAt ?? DateTime.UtcNow), ct);
        return count == 0 ? NotFound() : NoContent();
    }
}
```

## `src/Api/DTOs/Contracts.cs`

```csharp
namespace AussieAlerts.DTOs;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, string Email, string Role);
public sealed record AlertRequest(string? Suburb, string? Postcode, decimal MinPrice,
    decimal MaxPrice, int MinBedrooms, string? PropertyType, bool Active);
public sealed record AlertDto(Guid Id, string? Suburb, string? Postcode, decimal MinPrice,
    decimal MaxPrice, int MinBedrooms, string? PropertyType, bool Active, DateTime ActiveSince);
public sealed record ListingRequest(string Title, string Suburb, string Postcode,
    decimal Price, int Bedrooms, string PropertyType);
public sealed record ListingDto(Guid Id, string Title, string Suburb, string Postcode,
    decimal Price, int Bedrooms, string PropertyType, DateTime CreatedAt);
public sealed record NotificationDto(Guid Id, Guid AlertId, Guid ListingId,
    string Subject, string Body, DateTime CreatedAt, DateTime? ReadAt);
public sealed record ListingPage(List<ListingDto> Items, int Total, int Page, int PageSize);
public sealed record NotificationPage(List<NotificationDto> Items, int Total, int Page, int PageSize);
public sealed record ListingQuery(string? Postcode = null, decimal? MinPrice = null,
    decimal? MaxPrice = null, int Page = 1, int PageSize = 20);
public sealed record PageQuery(int Page = 1, int PageSize = 20);
```

## `src/Api/DTOs/Validators.cs`

```csharp
using FluentValidation;
namespace AussieAlerts.DTOs;

public static class InputRules
{
    public static readonly string[] Types = ["house", "unit", "townhouse", "land"];
    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128)
            .Matches("[a-z]").Matches("[A-Z]").Matches("[0-9]");
    }
}
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
public sealed class AlertValidator : AbstractValidator<AlertRequest>
{
    public AlertValidator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Suburb) || !string.IsNullOrWhiteSpace(x.Postcode))
            .WithMessage("Enter a suburb or postcode.");
        RuleFor(x => x.Suburb).MaximumLength(80);
        RuleFor(x => x.Postcode).Matches("^[0-9]{4}$").When(x => !string.IsNullOrEmpty(x.Postcode));
        RuleFor(x => x.MinPrice).InclusiveBetween(0, 100_000_000);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice).LessThanOrEqualTo(100_000_000);
        RuleFor(x => x.MinBedrooms).InclusiveBetween(0, 20);
        RuleFor(x => x.PropertyType).Must(x => InputRules.Types.Contains(x!)).When(x => !string.IsNullOrEmpty(x.PropertyType));
    }
}
public sealed class ListingValidator : AbstractValidator<ListingRequest>
{
    public ListingValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Suburb).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Postcode).NotEmpty().Matches("^[0-9]{4}$");
        RuleFor(x => x.Price).InclusiveBetween(0, 100_000_000);
        RuleFor(x => x.Bedrooms).InclusiveBetween(0, 20);
        RuleFor(x => x.PropertyType).Must(x => InputRules.Types.Contains(x));
    }
}
public sealed class ListingQueryValidator : AbstractValidator<ListingQuery>
{
    public ListingQueryValidator()
    {
        RuleFor(x => x.Postcode).Matches("^[0-9]{4}$").When(x => x.Postcode != null);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => !x.MinPrice.HasValue || !x.MaxPrice.HasValue || x.MinPrice <= x.MaxPrice)
            .WithMessage("Maximum price must be at least minimum price.");
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class PageValidator : AbstractValidator<PageQuery>
{
    public PageValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
```

## `src/Api/Data/AppDb.cs`

```csharp
using AussieAlerts.Domain;
using Microsoft.EntityFrameworkCore;

namespace AussieAlerts.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<PropertyAlert> Alerts => Set<PropertyAlert>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasMaxLength(20);
        });
        b.Entity<Listing>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Suburb).HasMaxLength(80);
            e.Property(x => x.Postcode).HasMaxLength(4);
            e.Property(x => x.PropertyType).HasMaxLength(20);
            e.Property(x => x.Price).HasPrecision(14, 2);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("clock_timestamp()");
            e.HasIndex(x => new { x.Postcode, x.Price });
            e.HasIndex(x => x.Price);
            e.HasIndex(x => x.CreatedAt);
            e.ToTable(t => t.HasCheckConstraint("CK_Listings_Values",
                "\"Price\" >= 0 AND \"Bedrooms\" BETWEEN 0 AND 20"));
        });
        b.Entity<PropertyAlert>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Suburb).HasMaxLength(80);
            e.Property(x => x.Postcode).HasMaxLength(4);
            e.Property(x => x.PropertyType).HasMaxLength(20);
            e.Property(x => x.MinPrice).HasPrecision(14, 2);
            e.Property(x => x.MaxPrice).HasPrecision(14, 2);
            e.Property(x => x.ActiveSince).HasDefaultValueSql("clock_timestamp()");
            e.HasIndex(x => new { x.Active, x.Postcode });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_Alerts_Prices",
                "\"MinPrice\" >= 0 AND \"MaxPrice\" >= \"MinPrice\""));
        });
        b.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(2000);
            e.HasIndex(x => new { x.AlertId, x.ListingId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PropertyAlert>().WithMany().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

## `src/Api/Data/AppDbFactory.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace AussieAlerts.Data;

public sealed class AppDbFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDb>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=alerts;Username=alerts;Password=local-design-only")
        .Options);
}
```

## `src/Api/Data/Migrations/20260928032017_InitialCreate.Designer.cs`

```csharp
﻿// <auto-generated />
using System;
using AussieAlerts.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AussieAlerts.Data.Migrations
{
    [DbContext(typeof(AppDb))]
    [Migration("20260928032017_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("AussieAlerts.Domain.AppUser", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(254)
                        .HasColumnType("character varying(254)");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("AussieAlerts.Domain.Listing", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("Bedrooms")
                        .HasColumnType("integer");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("clock_timestamp()");

                    b.Property<string>("Postcode")
                        .IsRequired()
                        .HasMaxLength(4)
                        .HasColumnType("character varying(4)");

                    b.Property<decimal>("Price")
                        .HasPrecision(14, 2)
                        .HasColumnType("numeric(14,2)");

                    b.Property<string>("PropertyType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Suburb")
                        .IsRequired()
                        .HasMaxLength(80)
                        .HasColumnType("character varying(80)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("Price");

                    b.HasIndex("Postcode", "Price");

                    b.ToTable("Listings", t =>
                        {
                            t.HasCheckConstraint("CK_Listings_Values", "\"Price\" >= 0 AND \"Bedrooms\" BETWEEN 0 AND 20");
                        });
                });

            modelBuilder.Entity("AussieAlerts.Domain.Notification", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AlertId")
                        .HasColumnType("uuid");

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("ListingId")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("ReadAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Subject")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("ListingId");

                    b.HasIndex("AlertId", "ListingId")
                        .IsUnique();

                    b.HasIndex("UserId", "CreatedAt");

                    b.ToTable("Notifications");
                });

            modelBuilder.Entity("AussieAlerts.Domain.PropertyAlert", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<DateTime>("ActiveSince")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("clock_timestamp()");

                    b.Property<decimal>("MaxPrice")
                        .HasPrecision(14, 2)
                        .HasColumnType("numeric(14,2)");

                    b.Property<int>("MinBedrooms")
                        .HasColumnType("integer");

                    b.Property<decimal>("MinPrice")
                        .HasPrecision(14, 2)
                        .HasColumnType("numeric(14,2)");

                    b.Property<string>("Postcode")
                        .HasMaxLength(4)
                        .HasColumnType("character varying(4)");

                    b.Property<string>("PropertyType")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Suburb")
                        .HasMaxLength(80)
                        .HasColumnType("character varying(80)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.HasIndex("Active", "Postcode");

                    b.ToTable("Alerts", t =>
                        {
                            t.HasCheckConstraint("CK_Alerts_Prices", "\"MinPrice\" >= 0 AND \"MaxPrice\" >= \"MinPrice\"");
                        });
                });

            modelBuilder.Entity("AussieAlerts.Domain.Notification", b =>
                {
                    b.HasOne("AussieAlerts.Domain.PropertyAlert", null)
                        .WithMany()
                        .HasForeignKey("AlertId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("AussieAlerts.Domain.Listing", null)
                        .WithMany()
                        .HasForeignKey("ListingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("AussieAlerts.Domain.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("AussieAlerts.Domain.PropertyAlert", b =>
                {
                    b.HasOne("AussieAlerts.Domain.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
```

## `src/Api/Data/Migrations/20260928032017_InitialCreate.cs`

```csharp
﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AussieAlerts.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Listings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Suburb = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Postcode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Bedrooms = table.Column<int>(type: "integer", nullable: false),
                    PropertyType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.Id);
                    table.CheckConstraint("CK_Listings_Values", "\"Price\" >= 0 AND \"Bedrooms\" BETWEEN 0 AND 20");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Suburb = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Postcode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    MinPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    MaxPrice = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    MinBedrooms = table.Column<int>(type: "integer", nullable: false),
                    PropertyType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.CheckConstraint("CK_Alerts_Prices", "\"MinPrice\" >= 0 AND \"MaxPrice\" >= \"MinPrice\"");
                    table.ForeignKey(
                        name: "FK_Alerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Alerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "Alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Active_Postcode",
                table: "Alerts",
                columns: new[] { "Active", "Postcode" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId",
                table: "Alerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_CreatedAt",
                table: "Listings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Postcode_Price",
                table: "Listings",
                columns: new[] { "Postcode", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Price",
                table: "Listings",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AlertId_ListingId",
                table: "Notifications",
                columns: new[] { "AlertId", "ListingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ListingId",
                table: "Notifications",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "Listings");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
```

## `src/Api/Data/Migrations/AppDbModelSnapshot.cs`

```csharp
﻿// <auto-generated />
using System;
using AussieAlerts.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AussieAlerts.Data.Migrations
{
    [DbContext(typeof(AppDb))]
    partial class AppDbModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("AussieAlerts.Domain.AppUser", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(254)
                        .HasColumnType("character varying(254)");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("AussieAlerts.Domain.Listing", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("Bedrooms")
                        .HasColumnType("integer");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("clock_timestamp()");

                    b.Property<string>("Postcode")
                        .IsRequired()
                        .HasMaxLength(4)
                        .HasColumnType("character varying(4)");

                    b.Property<decimal>("Price")
                        .HasPrecision(14, 2)
                        .HasColumnType("numeric(14,2)");

                    b.Property<string>("PropertyType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Suburb")
                        .IsRequired()
                        .HasMaxLength(80)
                        .HasColumnType("character varying(80)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("Price");

                    b.HasIndex("Postcode", "Price");

                    b.ToTable("Listings", t =>
                        {
                            t.HasCheckConstraint("CK_Listings_Values", "\"Price\" >= 0 AND \"Bedrooms\" BETWEEN 0 AND 20");
                        });
                });

            modelBuilder.Entity("AussieAlerts.Domain.Notification", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AlertId")
                        .HasColumnType("uuid");

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("ListingId")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("ReadAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Subject")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("ListingId");

                    b.HasIndex("AlertId", "ListingId")
                        .IsUnique();

                    b.HasIndex("UserId", "CreatedAt");

                    b.ToTable("Notifications");
                });

            modelBuilder.Entity("AussieAlerts.Domain.PropertyAlert", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<DateTime>("ActiveSince")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("clock_timestamp()");

                    b.Property<decimal>("MaxPrice")
                        .HasPrecision(14, 2)
                        .HasColumnType("numeric(14,2)");

                    b.Property<int>("MinBedrooms")
                        .HasColumnType("integer");

                    b.Property<decimal>("MinPrice")
                        .HasPrecision(14, 2)
                        .HasColumnType("numeric(14,2)");

                    b.Property<string>("Postcode")
                        .HasMaxLength(4)
                        .HasColumnType("character varying(4)");

                    b.Property<string>("PropertyType")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("Suburb")
                        .HasMaxLength(80)
                        .HasColumnType("character varying(80)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.HasIndex("Active", "Postcode");

                    b.ToTable("Alerts", t =>
                        {
                            t.HasCheckConstraint("CK_Alerts_Prices", "\"MinPrice\" >= 0 AND \"MaxPrice\" >= \"MinPrice\"");
                        });
                });

            modelBuilder.Entity("AussieAlerts.Domain.Notification", b =>
                {
                    b.HasOne("AussieAlerts.Domain.PropertyAlert", null)
                        .WithMany()
                        .HasForeignKey("AlertId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("AussieAlerts.Domain.Listing", null)
                        .WithMany()
                        .HasForeignKey("ListingId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("AussieAlerts.Domain.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("AussieAlerts.Domain.PropertyAlert", b =>
                {
                    b.HasOne("AussieAlerts.Domain.AppUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
```

## `src/Api/Domain/Entities.cs`

```csharp
namespace AussieAlerts.Domain;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
}

public sealed class Listing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Suburb { get; set; } = "";
    public string Postcode { get; set; } = "";
    public decimal Price { get; set; }
    public int Bedrooms { get; set; }
    public string PropertyType { get; set; } = "house";
    public DateTime CreatedAt { get; set; }
}

public sealed class PropertyAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public int MinBedrooms { get; set; }
    public string? PropertyType { get; set; }
    public bool Active { get; set; } = true;
    public DateTime ActiveSince { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AlertId { get; set; }
    public Guid ListingId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
```

## `src/Api/Infrastructure/Errors.cs`

```csharp
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace AussieAlerts.Infrastructure;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && ct.IsCancellationRequested) return false;
        var status = exception switch
        {
            ValidationException => 400, ApiException e => e.Status, BadHttpRequestException => 400, _ => 500
        };
        ProblemDetails problem = exception is ValidationException v
            ? new ValidationProblemDetails(v.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = status == 500 ? "An unexpected error occurred." : exception is ValidationException ? "Validation failed." : exception.Message;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (status == 500) logger.LogError(exception, "Unhandled failure {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, cancellationToken: ct);
        return true;
    }
}
```

## `src/Api/Program.cs`

```csharp
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
```

## `src/Api/Repositories/AlertRepository.cs`

```csharp
using AussieAlerts.Data;
using AussieAlerts.Domain;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Repositories;

// A focused repository for owner-scoped alert access. DbContext already acts as a unit of work.
public sealed class AlertRepository(AppDb db)
{
    public Task<List<PropertyAlert>> ListAsync(Guid owner, CancellationToken ct) =>
        db.Alerts.AsNoTracking().Where(x => x.UserId == owner).OrderByDescending(x => x.ActiveSince).ToListAsync(ct);
    public Task<PropertyAlert?> FindAsync(Guid owner, Guid id, CancellationToken ct) =>
        db.Alerts.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
    public void Add(PropertyAlert alert) => db.Alerts.Add(alert);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public Task<DateTime> NowAsync(CancellationToken ct) =>
        db.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);
}
```

## `src/Api/Services/AdminBootstrap.cs`

```csharp
using FluentValidation;
using AussieAlerts.Data;
using AussieAlerts.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Services;

public static class AdminBootstrap
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Seed admin credentials are required when bootstrap is enabled.");
        await new DTOs.RegisterValidator().ValidateAndThrowAsync(new(email, password));
        var db = services.GetRequiredService<AppDb>();
        email = email.Trim().ToLowerInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (existing != null)
        {
            if (existing.Role != "Admin") throw new InvalidOperationException("Bootstrap email belongs to a non-admin; refusing to promote it.");
            return;
        }
        var user = new AppUser { Email = email, Role = "Admin" };
        user.PasswordHash = services.GetRequiredService<IPasswordHasher<AppUser>>().HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
```

## `src/Api/Services/AlertService.cs`

```csharp
using AussieAlerts.Domain;
using AussieAlerts.DTOs;
using AussieAlerts.Infrastructure;
using AussieAlerts.Repositories;
namespace AussieAlerts.Services;

public sealed class AlertService(AlertRepository repo)
{
    public async Task<List<AlertDto>> ListAsync(Guid owner, CancellationToken ct) =>
        (await repo.ListAsync(owner, ct)).Select(Map).ToList();
    public async Task<AlertDto> CreateAsync(Guid owner, AlertRequest request, CancellationToken ct)
    {
        var alert = new PropertyAlert { UserId = owner };
        Apply(alert, request);
        repo.Add(alert); // Database generates ActiveSince using its own UTC clock.
        await repo.SaveAsync(ct);
        return Map(alert);
    }
    public async Task<AlertDto> UpdateAsync(Guid owner, Guid id, AlertRequest request, CancellationToken ct)
    {
        var alert = await repo.FindAsync(owner, id, ct) ?? throw new ApiException(404, "Alert not found.");
        Apply(alert, request);
        // Editing starts a new matching window; old notifications are retained.
        alert.ActiveSince = await repo.NowAsync(ct);
        await repo.SaveAsync(ct);
        return Map(alert);
    }
    private static void Apply(PropertyAlert a, AlertRequest r)
    {
        a.Suburb = InputRules.Normalize(r.Suburb); a.Postcode = InputRules.Normalize(r.Postcode);
        a.MinPrice = r.MinPrice; a.MaxPrice = r.MaxPrice; a.MinBedrooms = r.MinBedrooms;
        a.PropertyType = InputRules.Normalize(r.PropertyType); a.Active = r.Active;
    }
    private static AlertDto Map(PropertyAlert a) => new(a.Id, a.Suburb, a.Postcode,
        a.MinPrice, a.MaxPrice, a.MinBedrooms, a.PropertyType, a.Active, a.ActiveSince);
}

public static class AlertMatcher
{
    // Reference rule used by unit tests; the bulk SQL implementation has database integration tests.
    public static bool Matches(PropertyAlert a, Listing l) => a.Active && l.CreatedAt >= a.ActiveSince
        && (a.Suburb == null || a.Suburb == l.Suburb)
        && (a.Postcode == null || a.Postcode == l.Postcode)
        && l.Price >= a.MinPrice && l.Price <= a.MaxPrice
        && l.Bedrooms >= a.MinBedrooms && (a.PropertyType == null || a.PropertyType == l.PropertyType);
}
```

## `src/Api/Services/AuthService.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AussieAlerts.Data;
using AussieAlerts.Domain;
using AussieAlerts.DTOs;
using AussieAlerts.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
namespace AussieAlerts.Services;

public sealed class AuthService(AppDb db, IPasswordHasher<AppUser> hasher, IConfiguration config)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = new AppUser { Email = request.Email.Trim().ToLowerInvariant() };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user); // Public registration can NEVER choose a role.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ApiException(409, "An account already uses this email."); }
        return Issue(user);
    }
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        var result = user == null ? PasswordVerificationResult.Failed : hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (user == null || result == PasswordVerificationResult.Failed) throw new ApiException(401, "Invalid email or password.");
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }
        return Issue(user);
    }
    private AuthResponse Issue(AppUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(30);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
            [new Claim("sub", user.Id.ToString()), new Claim("role", user.Role)],
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, user.Email, user.Role);
    }
}
```

## `src/Api/Services/MatchService.cs`

```csharp
using AussieAlerts.Data;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Services;

public sealed class MatchService(AppDb db)
{
    // One atomic statement: retries, concurrent workers and restarts cannot duplicate an alert/listing pair.
    // No moving watermark: late commits are revisited on the next poll.
    public Task<int> RunAsync(CancellationToken ct = default) => db.Database.ExecuteSqlRawAsync("""
        INSERT INTO "Notifications" ("Id", "UserId", "AlertId", "ListingId", "Subject", "Body", "CreatedAt")
        SELECT gen_random_uuid(), a."UserId", a."Id", l."Id",
               'New fake listing: ' || l."Title",
               'SIMULATED EMAIL ONLY. ' || l."Suburb" || ' ' || l."Postcode" ||
               ', AUD ' || l."Price"::text || ', ' || l."Bedrooms"::text ||
               ' bedrooms, ' || l."PropertyType" || '. No real property or email delivery.',
               clock_timestamp()
        FROM "Alerts" a JOIN "Listings" l
          ON l."CreatedAt" >= a."ActiveSince"
         AND (a."Postcode" IS NULL OR a."Postcode" = l."Postcode")
         AND (a."Suburb" IS NULL OR a."Suburb" = l."Suburb")
         AND l."Price" BETWEEN a."MinPrice" AND a."MaxPrice"
         AND l."Bedrooms" >= a."MinBedrooms"
         AND (a."PropertyType" IS NULL OR a."PropertyType" = l."PropertyType")
        WHERE a."Active" = TRUE
          AND NOT EXISTS (SELECT 1 FROM "Notifications" n
                          WHERE n."AlertId" = a."Id" AND n."ListingId" = l."Id")
        ON CONFLICT ("AlertId", "ListingId") DO NOTHING;
        """, ct);
}

public sealed class MatchingWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<MatchingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Matching:Enabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.GetValue("Matching:IntervalSeconds", 15)));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var count = await scope.ServiceProvider.GetRequiredService<MatchService>().RunAsync(stoppingToken);
                    if (count > 0) logger.LogInformation("Created {Count} simulated notifications", count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Matching failed; next scheduled poll will retry"); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
```

## `src/Api/appsettings.json`

```json
{
  "Jwt": {
    "Issuer": "aussie-alerts",
    "Audience": "aussie-alerts-web"
  },
  "Frontend": {
    "Origin": "http://localhost:5173"
  },
  "Matching": {
    "Enabled": true,
    "IntervalSeconds": 15
  },
  "Database": {
    "AutoMigrate": false
  },
  "Seed": {
    "Enabled": false
  },
  "AllowedHosts": "*"
}
```

## `tests/Api.Tests/Api.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><IsTestProject>true</IsTestProject><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup><ProjectReference Include="../../src/Api/Api.csproj" /></ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2"><PrivateAssets>all</PrivateAssets></PackageReference>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.15.0" />
  </ItemGroup>
  <ItemGroup><Using Include="Xunit" /></ItemGroup>
</Project>
```

## `tests/Api.Tests/ApiFactory.cs`

```csharp
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
```

## `tests/Api.Tests/ContractTests.cs`

```csharp
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
```

## `tests/Api.Tests/IntegrationTests.cs`

```csharp
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
```

## `tests/Api.Tests/MatchingTests.cs`

```csharp
using AussieAlerts.Domain;
using AussieAlerts.Services;
using AussieAlerts.DTOs;
namespace Api.Tests;
public sealed class MatchingTests
{
    [Theory]
    [InlineData(100, 2, true)]
    [InlineData(200, 3, true)]
    [InlineData(99, 2, false)]
    [InlineData(201, 2, false)]
    [InlineData(150, 1, false)]
    public void Price_is_inclusive_and_bedrooms_is_minimum(int price, int beds, bool expected)
    {
        var start = DateTime.UtcNow;
        var alert = new PropertyAlert { Postcode="0800", MinPrice=100, MaxPrice=200, MinBedrooms=2, ActiveSince=start };
        var listing = new Listing { Postcode="0800", Price=price, Bedrooms=beds, CreatedAt=start.AddSeconds(1) };
        Assert.Equal(expected, AlertMatcher.Matches(alert, listing));
    }
    [Fact]
    public void Both_locations_type_active_and_future_time_are_required()
    {
        var start=DateTime.UtcNow;
        var a = new PropertyAlert { Suburb="darwin", Postcode="0800", PropertyType="unit", MaxPrice=1000, ActiveSince=start };
        var l = new Listing { Suburb="darwin", Postcode="0800", PropertyType="unit", Price=500, CreatedAt=start.AddSeconds(1) };
        Assert.True(AlertMatcher.Matches(a,l));
        l.Suburb="other"; Assert.False(AlertMatcher.Matches(a,l)); l.Suburb="darwin";
        l.Postcode="2000"; Assert.False(AlertMatcher.Matches(a,l)); l.Postcode="0800";
        l.PropertyType="house"; Assert.False(AlertMatcher.Matches(a,l)); l.PropertyType="unit";
        l.CreatedAt=start.AddSeconds(-1); Assert.False(AlertMatcher.Matches(a,l)); l.CreatedAt=start.AddSeconds(1);
        a.Active=false; Assert.False(AlertMatcher.Matches(a,l));
    }
    [Fact]
    public void Validation_preserves_leading_zero_and_rejects_empty_location()
    {
        var v = new AlertValidator();
        Assert.True(v.Validate(new AlertRequest(null,"0800",0,200,2,null,true)).IsValid);
        Assert.False(v.Validate(new AlertRequest(null,null,0,200,2,null,true)).IsValid);
        Assert.False(v.Validate(new AlertRequest(null,"800",0,200,2,null,true)).IsValid);
        Assert.False(v.Validate(new AlertRequest(null,"0800",300,200,2,null,true)).IsValid);
    }
}
```

## `web/.dockerignore`

```text
node_modules
dist
coverage
```

## `web/Dockerfile.dev`

```text
FROM node:24-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
EXPOSE 5173
CMD ["npm", "run", "dev"]
```

## `web/eslint.config.js`

```javascript
import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import hooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
export default tseslint.config(
  { ignores: ['dist', 'src/api/schema.d.ts'] },
  js.configs.recommended, ...tseslint.configs.recommended,
  { files: ['**/*.{ts,tsx}'], languageOptions: { globals: { ...globals.browser, ...globals.node } },
    plugins: { 'react-hooks': hooks }, rules: hooks.configs.recommended.rules },
);
```

## `web/index.html`

```html
<!doctype html><html lang="en"><head><meta charset="UTF-8"/><meta name="viewport" content="width=device-width, initial-scale=1.0"/><title>Aussie Property Alerts — Fake Data Demo</title></head><body><div id="root"></div><script type="module" src="/src/main.tsx"></script></body></html>
```

## `web/package.json`

```json
{
  "name": "aussie-alerts-web",
  "private": true,
  "version": "1.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite --host 0.0.0.0",
    "build": "tsc -b && vite build",
    "preview": "vite preview --host 0.0.0.0",
    "lint": "eslint .",
    "test": "vitest run",
    "generate:api": "openapi-typescript ../openapi.json -o src/api/schema.d.ts"
  },
  "dependencies": {
    "@hookform/resolvers": "3.10.0",
    "@tanstack/react-query": "5.66.0",
    "openapi-fetch": "0.13.5",
    "react": "19.0.0",
    "react-dom": "19.0.0",
    "react-hook-form": "7.54.2",
    "react-router-dom": "7.18.4",
    "zod": "3.24.2"
  },
  "devDependencies": {
    "@eslint/js": "9.39.5",
    "@testing-library/jest-dom": "6.6.3",
    "@testing-library/react": "16.1.0",
    "@testing-library/user-event": "14.6.1",
    "@types/node": "22.13.1",
    "@types/react": "19.0.8",
    "@types/react-dom": "19.0.3",
    "@vitejs/plugin-react": "4.7.0",
    "eslint": "9.39.5",
    "eslint-plugin-react-hooks": "5.1.0",
    "globals": "15.14.0",
    "jsdom": "26.0.0",
    "openapi-typescript": "7.6.1",
    "typescript": "5.7.3",
    "typescript-eslint": "8.24.0",
    "vite": "6.4.3",
    "vitest": "4.1.11"
  }
}
```

## `web/src/App.tsx`

```tsx
import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Link, Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useSession, setSession } from './session';
import { AuthPage } from './pages/AuthPage';
import { AlertsPage } from './pages/AlertsPage';
import { ListingsPage } from './pages/ListingsPage';
import { InboxPage } from './pages/InboxPage';
function Protected() { return useSession() ? <Outlet/> : <Navigate to="/login" replace/>; }
export default function App() {
  const session = useSession(); const cache = useQueryClient();
  useEffect(() => { if (!session) cache.clear(); }, [session, cache]);
  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(() => setSession(null), Math.max(0, Date.parse(session.expiresAt) - Date.now()));
    return () => window.clearTimeout(timer);
  }, [session]);
  return <><header><Link className="brand" to="/alerts">Aussie<span>Alerts</span></Link><nav aria-label="Main navigation">
    {session ? <><Link to="/alerts">Alerts</Link><Link to="/listings">Listings</Link><Link to="/inbox">Inbox</Link><button className="secondary" onClick={() => {setSession(null); cache.clear();}}>Log out</button></>
      : <><Link to="/login">Log in</Link><Link to="/register">Register</Link></>}
  </nav></header><div className="notice">Learning project · Fake Australian listings only · No real email delivery</div><main>
    <Routes><Route path="/login" element={<AuthPage key="login"/>}/><Route path="/register" element={<AuthPage key="register" register/>}/>
      <Route element={<Protected/>}><Route path="/alerts" element={<AlertsPage/>}/><Route path="/listings" element={<ListingsPage/>}/><Route path="/inbox" element={<InboxPage/>}/></Route>
      <Route path="*" element={<Navigate to="/alerts" replace/>}/></Routes>
  </main><footer>C# / ASP.NET Core · React + TypeScript · PostgreSQL</footer></>;
}
```

## `web/src/api/client.ts`

```typescript
import createClient from 'openapi-fetch';
import type { paths, components } from './schema';
import { getSession, setSession } from '../session';
export type Alert = components['schemas']['AlertDto'];
export type AlertInput = components['schemas']['AlertRequest'];
export type ListingInput = components['schemas']['ListingRequest'];
const client = createClient<paths>({ baseUrl: import.meta.env.VITE_API_URL ?? '' });
client.use({
  onRequest({ request }) {
    const token = getSession()?.accessToken;
    if (token) request.headers.set('Authorization', `Bearer ${token}`);
    return request;
  },
  onResponse({ response }) {
    if (response.status === 401 && getSession()) setSession(null);
    return response;
  },
});
function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (!result.response.ok) {
    const error = result.error as { title?: string; errors?: Record<string, string[]> } | undefined;
    const messages = error?.errors ? Object.values(error.errors).flat().join(' ') : '';
    throw new Error(messages || error?.title || `Request failed (${result.response.status}).`);
  }
  return result.data as T;
}
export const api = {
  login: async (email: string, password: string) => unwrap(await client.POST('/api/auth/login', { body: { email, password } })),
  register: async (email: string, password: string) => unwrap(await client.POST('/api/auth/register', { body: { email, password } })),
  alerts: async () => unwrap(await client.GET('/api/alerts')),
  createAlert: async (body: AlertInput) => unwrap(await client.POST('/api/alerts', { body })),
  updateAlert: async (id: string, body: AlertInput) => unwrap(await client.PUT('/api/alerts/{id}', { params: { path: { id } }, body })),
  listings: async (postcode: string, page: number) => unwrap(await client.GET('/api/listings', {
    params: { query: { Postcode: postcode || undefined, Page: page, PageSize: 12 } },
  })),
  addListing: async (body: ListingInput) => unwrap(await client.POST('/api/admin/listings', { body })),
  notifications: async (page: number) => unwrap(await client.GET('/api/notifications', { params: { query: { Page: page, PageSize: 20 } } })),
  markRead: async (id: string) => unwrap(await client.PUT('/api/notifications/{id}/read', { params: { path: { id } } })),
};
```

## `web/src/api/schema.d.ts`

```typescript
/**
 * This file was auto-generated by openapi-typescript.
 * Do not make direct changes to the file.
 */

export interface paths {
    "/api/alerts": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["AlertDto"][];
                        "application/json": components["schemas"]["AlertDto"][];
                        "text/json": components["schemas"]["AlertDto"][];
                    };
                };
            };
        };
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["AlertRequest"];
                    "text/json": components["schemas"]["AlertRequest"];
                    "application/*+json": components["schemas"]["AlertRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["AlertDto"];
                        "application/json": components["schemas"]["AlertDto"];
                        "text/json": components["schemas"]["AlertDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/alerts/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["AlertRequest"];
                    "text/json": components["schemas"]["AlertRequest"];
                    "application/*+json": components["schemas"]["AlertRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["AlertDto"];
                        "application/json": components["schemas"]["AlertDto"];
                        "text/json": components["schemas"]["AlertDto"];
                    };
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/auth/register": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["RegisterRequest"];
                    "text/json": components["schemas"]["RegisterRequest"];
                    "application/*+json": components["schemas"]["RegisterRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["AuthResponse"];
                        "application/json": components["schemas"]["AuthResponse"];
                        "text/json": components["schemas"]["AuthResponse"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/auth/login": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["LoginRequest"];
                    "text/json": components["schemas"]["LoginRequest"];
                    "application/*+json": components["schemas"]["LoginRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["AuthResponse"];
                        "application/json": components["schemas"]["AuthResponse"];
                        "text/json": components["schemas"]["AuthResponse"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/listings": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    Postcode?: string;
                    MinPrice?: number;
                    MaxPrice?: number;
                    Page?: number;
                    PageSize?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["ListingPage"];
                        "application/json": components["schemas"]["ListingPage"];
                        "text/json": components["schemas"]["ListingPage"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/admin/listings": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": components["schemas"]["ListingRequest"];
                    "text/json": components["schemas"]["ListingRequest"];
                    "application/*+json": components["schemas"]["ListingRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["ListingDto"];
                        "application/json": components["schemas"]["ListingDto"];
                        "text/json": components["schemas"]["ListingDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/notifications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    Page?: number;
                    PageSize?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["NotificationPage"];
                        "application/json": components["schemas"]["NotificationPage"];
                        "text/json": components["schemas"]["NotificationPage"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/notifications/{id}/read": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        AlertDto: {
            /** Format: uuid */
            id: string;
            suburb: null | string;
            postcode: null | string;
            /** Format: double */
            minPrice: number;
            /** Format: double */
            maxPrice: number;
            /** Format: int32 */
            minBedrooms: number;
            propertyType: null | string;
            active: boolean;
            /** Format: date-time */
            activeSince: string;
        };
        AlertRequest: {
            suburb: null | string;
            postcode: null | string;
            /** Format: double */
            minPrice: number;
            /** Format: double */
            maxPrice: number;
            /** Format: int32 */
            minBedrooms: number;
            propertyType: null | string;
            active: boolean;
        };
        AuthResponse: {
            accessToken: string;
            /** Format: date-time */
            expiresAt: string;
            email: string;
            role: string;
        };
        ListingDto: {
            /** Format: uuid */
            id: string;
            title: string;
            suburb: string;
            postcode: string;
            /** Format: double */
            price: number;
            /** Format: int32 */
            bedrooms: number;
            propertyType: string;
            /** Format: date-time */
            createdAt: string;
        };
        ListingPage: {
            items: components["schemas"]["ListingDto"][];
            /** Format: int32 */
            total: number;
            /** Format: int32 */
            page: number;
            /** Format: int32 */
            pageSize: number;
        };
        ListingRequest: {
            title: string;
            suburb: string;
            postcode: string;
            /** Format: double */
            price: number;
            /** Format: int32 */
            bedrooms: number;
            propertyType: string;
        };
        LoginRequest: {
            email: string;
            password: string;
        };
        NotificationDto: {
            /** Format: uuid */
            id: string;
            /** Format: uuid */
            alertId: string;
            /** Format: uuid */
            listingId: string;
            subject: string;
            body: string;
            /** Format: date-time */
            createdAt: string;
            /** Format: date-time */
            readAt: null | string;
        };
        NotificationPage: {
            items: components["schemas"]["NotificationDto"][];
            /** Format: int32 */
            total: number;
            /** Format: int32 */
            page: number;
            /** Format: int32 */
            pageSize: number;
        };
        RegisterRequest: {
            email: string;
            password: string;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;
```

## `web/src/components.tsx`

```tsx
export function ErrorMessage({ error }: { error: unknown }) {
  return error ? <p role="alert" className="error">{error instanceof Error ? error.message : 'Something went wrong.'}</p> : null;
}
export function Pagination({ page, total, size, change }: { page: number; total: number; size: number; change: (n: number) => void }) {
  return <div className="pagination"><button disabled={page <= 1} onClick={() => change(page - 1)}>Previous</button>
    <span>Page {page} · {total} results</span><button disabled={page * size >= total} onClick={() => change(page + 1)}>Next</button></div>;
}
export const money = (value: number) => new Intl.NumberFormat('en-AU', { style: 'currency', currency: 'AUD', maximumFractionDigits: 0 }).format(value);
```

## `web/src/forms.ts`

```typescript
import { z } from 'zod';
export const propertyTypes = ['house', 'unit', 'townhouse', 'land'] as const;
export const alertSchema = z.object({
  suburb: z.string().trim().max(80),
  postcode: z.string().regex(/^(|[0-9]{4})$/, 'Use four digits, such as 0800.'),
  minPrice: z.number().min(0).max(100_000_000),
  maxPrice: z.number().min(0).max(100_000_000),
  minBedrooms: z.number().int().min(0).max(20),
  propertyType: z.enum(['', ...propertyTypes]), active: z.boolean(),
}).refine(x => x.suburb.length > 0 || x.postcode.length > 0, { message: 'Enter a suburb or postcode.', path: ['suburb'] })
  .refine(x => x.maxPrice >= x.minPrice, { message: 'Maximum must be at least minimum.', path: ['maxPrice'] });
export type AlertValues = z.infer<typeof alertSchema>;
export const listingSchema = z.object({
  title: z.string().trim().min(1).max(150), suburb: z.string().trim().min(1).max(80),
  postcode: z.string().regex(/^[0-9]{4}$/, 'Use four digits.'),
  price: z.number().min(0).max(100_000_000), bedrooms: z.number().int().min(0).max(20),
  propertyType: z.enum(propertyTypes),
});
export type ListingValues = z.infer<typeof listingSchema>;
export function authSchema(register: boolean) {
  return z.object({ email: z.string().trim().email(), password: register
    ? z.string().min(12).max(128).regex(/[a-z]/, 'Add a lowercase letter.').regex(/[A-Z]/, 'Add an uppercase letter.').regex(/[0-9]/, 'Add a number.')
    : z.string().min(1).max(128) });
}
```

## `web/src/main.tsx`

```tsx
import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App';
import './style.css';
const client = new QueryClient({ defaultOptions: { queries: { retry: 1 } } });
ReactDOM.createRoot(document.getElementById('root')!).render(<React.StrictMode><QueryClientProvider client={client}><BrowserRouter><App/></BrowserRouter></QueryClientProvider></React.StrictMode>);
```

## `web/src/pages/AlertsPage.tsx`

```tsx
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type Alert, type AlertInput } from '../api/client';
import { alertSchema, propertyTypes, type AlertValues } from '../forms';
import { ErrorMessage, money } from '../components';

export function AlertForm({ alert, done }: { alert?: Alert; done: () => void }) {
  const cache = useQueryClient();
  const form = useForm<AlertValues>({ resolver: zodResolver(alertSchema), defaultValues: {
    suburb: alert?.suburb ?? '', postcode: alert?.postcode ?? '', minPrice: alert?.minPrice ?? 300000,
    maxPrice: alert?.maxPrice ?? 800000, minBedrooms: alert?.minBedrooms ?? 2,
    propertyType: (alert?.propertyType ?? '') as AlertValues['propertyType'], active: alert?.active ?? true,
  } });
  const mutation = useMutation({ mutationFn: (v: AlertValues) => {
    const body: AlertInput = { ...v, suburb: v.suburb || null, postcode: v.postcode || null, propertyType: v.propertyType || null };
    return alert ? api.updateAlert(alert.id, body) : api.createAlert(body);
  }, onSuccess: async () => { await cache.invalidateQueries({queryKey: ['alerts']}); done(); } });
  return <form className="panel" onSubmit={form.handleSubmit(v => mutation.mutate(v))} noValidate>
    <h2>{alert ? 'Edit alert' : 'Create alert'}</h2><p>Enter a suburb, a postcode, or both. New and edited alerts match future listings.</p>
    <div className="grid">
      <label>Suburb<input {...form.register('suburb')} placeholder="darwin"/></label>
      <label>Postcode<input {...form.register('postcode')} inputMode="numeric" placeholder="0800"/></label>
      <label>Minimum price (AUD)<input type="number" {...form.register('minPrice', { valueAsNumber: true })}/></label>
      <label>Maximum price (AUD)<input type="number" {...form.register('maxPrice', { valueAsNumber: true })}/></label>
      <label>Minimum bedrooms<input type="number" {...form.register('minBedrooms', { valueAsNumber: true })}/></label>
      <label>Property type<select {...form.register('propertyType')}><option value="">Any type</option>{propertyTypes.map(t => <option key={t}>{t}</option>)}</select></label>
    </div><label className="check"><input type="checkbox" {...form.register('active')}/>Active</label>
    {Object.entries(form.formState.errors).map(([key, e]) => <p role="alert" className="error" key={key}>{e.message}</p>)}
    <ErrorMessage error={mutation.error}/><div className="actions"><button disabled={mutation.isPending}>Save alert</button><button type="button" className="secondary" onClick={done}>Cancel</button></div>
  </form>;
}
export function AlertsPage() {
  const query = useQuery({ queryKey: ['alerts'], queryFn: api.alerts });
  const [editing, setEditing] = useState<Alert | 'new' | null>(null);
  return <><div className="heading"><div><h1>Your property alerts</h1><p>Choose what you want. Check the inbox for simulated emails.</p></div><button onClick={() => setEditing('new')}>New alert</button></div>
    {editing && <AlertForm key={editing === 'new' ? 'new' : editing.id} alert={editing === 'new' ? undefined : editing} done={() => setEditing(null)}/>}
    {query.isPending && <p>Loading alerts…</p>}<ErrorMessage error={query.error}/>
    {query.data?.length === 0 && <p className="panel">No alerts yet. Create your first alert.</p>}
    <div className="grid">{query.data?.map(a => <article className="panel" key={a.id}><span className="badge">{a.active ? 'Active' : 'Paused'}</span>
      <h2>{a.suburb || 'Any suburb'} {a.postcode}</h2><p>{money(a.minPrice)} – {money(a.maxPrice)}</p>
      <p>{a.minBedrooms}+ bedrooms · {a.propertyType || 'Any type'}</p><button className="secondary" onClick={() => setEditing(a)}>Edit</button></article>)}</div></>;
}
```

## `web/src/pages/AuthPage.tsx`

```tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { authSchema } from '../forms';
import { setSession } from '../session';
import { ErrorMessage } from '../components';
export function AuthPage({ register = false }: { register?: boolean }) {
  const navigate = useNavigate(); const cache = useQueryClient();
  const form = useForm<{email: string; password: string}>({ resolver: zodResolver(authSchema(register)) });
  const mutation = useMutation({ mutationFn: (v: {email: string; password: string}) =>
    register ? api.register(v.email, v.password) : api.login(v.email, v.password),
    onSuccess: data => { cache.clear(); setSession(data); navigate('/alerts'); },
  });
  return <section className="panel narrow"><h1>{register ? 'Create your account' : 'Welcome back'}</h1>
    <p>Use a fictional email for this learning project.</p>
    <form onSubmit={form.handleSubmit(v => mutation.mutate(v))} noValidate>
      <label>Email<input type="email" autoComplete="username" {...form.register('email')} /></label>
      <small role="status">{form.formState.errors.email?.message}</small>
      <label>Password<input type="password" autoComplete={register ? 'new-password' : 'current-password'} {...form.register('password')} /></label>
      {register && <small>12+ characters with uppercase, lowercase and a number.</small>}
      <small role="status">{form.formState.errors.password?.message}</small>
      <ErrorMessage error={mutation.error}/><button disabled={mutation.isPending}>{mutation.isPending ? 'Please wait…' : register ? 'Register' : 'Log in'}</button>
    </form><p><Link to={register ? '/login' : '/register'}>{register ? 'Already registered? Log in' : 'Create an account'}</Link></p>
  </section>;
}
```

## `web/src/pages/InboxPage.tsx`

```tsx
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { ErrorMessage, Pagination } from '../components';
export function InboxPage() {
  const [page, setPage] = useState(1); const cache = useQueryClient();
  const query = useQuery({queryKey:['notifications', page], queryFn: () => api.notifications(page), refetchInterval: 5000});
  const mark = useMutation({mutationFn:api.markRead, onSuccess: () => cache.invalidateQueries({queryKey:['notifications']})});
  return <><h1>Your notifications</h1><p>Simulated emails stored in PostgreSQL. No email is sent. Refreshes every 5 seconds.</p>
    {query.isPending && <p>Loading inbox…</p>}<ErrorMessage error={query.error || mark.error}/>
    {query.data?.items.length === 0 && <p className="panel">Your inbox is empty. Create an alert, then add a matching fake listing.</p>}
    {query.data?.items.map(n => <article className="panel" key={n.id}><span className="badge">{n.readAt ? 'Read' : 'Unread'}</span>
      <h2>{n.subject}</h2><p>{n.body}</p><small>{new Date(n.createdAt).toLocaleString()}</small>
      {!n.readAt && <p><button disabled={mark.isPending} onClick={() => mark.mutate(n.id)}>Mark as read</button></p>}</article>)}
    {query.data && <Pagination page={page} total={query.data.total} size={20} change={setPage}/>}</>;
}
```

## `web/src/pages/ListingsPage.tsx`

```tsx
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { listingSchema, propertyTypes, type ListingValues } from '../forms';
import { useSession } from '../session';
import { ErrorMessage, Pagination, money } from '../components';

function AddListing() {
  const cache = useQueryClient();
  const form = useForm<ListingValues>({ resolver: zodResolver(listingSchema), defaultValues: {
    title: 'Demo Darwin unit — fictional', suburb: 'darwin', postcode: '0800', price: 500000, bedrooms: 2, propertyType: 'unit',
  } });
  const mutation = useMutation({ mutationFn: api.addListing,
    onSuccess: () => cache.invalidateQueries({queryKey: ['listings']}) });
  return <details className="panel"><summary>Admin: add a fake listing</summary>
    <form onSubmit={form.handleSubmit(v => mutation.mutate(v))} noValidate><div className="grid">
      <label>Title<input {...form.register('title')}/></label><label>Suburb<input {...form.register('suburb')}/></label>
      <label>Postcode<input {...form.register('postcode')}/></label><label>Price (AUD)<input type="number" {...form.register('price', {valueAsNumber:true})}/></label>
      <label>Bedrooms<input type="number" {...form.register('bedrooms', {valueAsNumber:true})}/></label>
      <label>Type<select {...form.register('propertyType')}>{propertyTypes.map(t => <option key={t}>{t}</option>)}</select></label>
    </div>{Object.entries(form.formState.errors).map(([key, e]) => <p role="alert" key={key}>{e.message}</p>)}
      <ErrorMessage error={mutation.error}/>{mutation.isSuccess && <p role="status">Fake listing added. Matching runs every 15 seconds.</p>}
      <button disabled={mutation.isPending}>Add fake listing</button>
    </form></details>;
}
export function ListingsPage() {
  const [postcode, setPostcode] = useState(''); const [draft, setDraft] = useState(''); const [page, setPage] = useState(1);
  const session = useSession();
  const query = useQuery({queryKey:['listings', postcode, page], queryFn: () => api.listings(postcode, page)});
  return <><h1>Browse fake listings</h1><p>Every listing is fictional. Prices are in Australian dollars.</p>
    {session?.role === 'Admin' && <AddListing/>}
    <form className="search" onSubmit={e => {e.preventDefault(); setPostcode(draft); setPage(1);}}><label>Filter by postcode<input value={draft} onChange={e=>setDraft(e.target.value)} pattern="[0-9]{4}" placeholder="0800"/></label><button>Search</button></form>
    {query.isPending && <p>Loading listings…</p>}<ErrorMessage error={query.error}/>
    {query.data?.items.length === 0 && <p className="panel">No listings found. Ask the demo admin to add a fake listing.</p>}
    <div className="grid">{query.data?.items.map(l => <article className="panel" key={l.id}><span className="badge">Fictional listing</span><h2>{l.title}</h2>
      <p className="price">{money(l.price)}</p><p>{l.suburb} · {l.postcode}</p><p>{l.bedrooms} bedrooms · {l.propertyType}</p></article>)}</div>
    {query.data && <Pagination page={page} total={query.data.total} size={12} change={setPage}/>}</>;
}
```

## `web/src/session.ts`

```typescript
import { useSyncExternalStore } from 'react';
import type { components } from './api/schema';
type Session = components['schemas']['AuthResponse'];
let session: Session | null = null;
const listeners = new Set<() => void>();
export function setSession(value: Session | null) { session = value; listeners.forEach(fn => fn()); }
export function getSession() { return session; }
export function useSession() {
  return useSyncExternalStore(fn => { listeners.add(fn); return () => { listeners.delete(fn); }; }, getSession);
}
// Intentionally in memory: refresh requires login; no token is persisted in localStorage.
```

## `web/src/style.css`

```css
*{box-sizing:border-box}body{margin:0;background:#f4f6f4;color:#172f32;font:16px/1.6 system-ui,sans-serif}header{background:#fff;display:flex;align-items:center;justify-content:space-between;padding:20px max(5vw,20px);border-bottom:1px solid #dce4de;gap:20px;flex-wrap:wrap}.brand{font-size:25px;font-weight:800;text-decoration:none}.brand span{color:#128571}nav{display:flex;align-items:center;gap:22px}a{color:#126c5b}main{max-width:1120px;margin:40px auto;padding:0 24px;min-height:65vh}h1{font-size:32px;line-height:1.2}h2{font-size:21px;line-height:1.4}.notice{text-align:center;background:#dceddf;padding:9px 18px;font-size:13px}.panel{background:white;border:1px solid #dce4de;border-radius:14px;padding:26px;margin-bottom:22px}.narrow{max-width:480px;margin:0 auto}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:20px}.grid .panel{margin:0}.heading{display:flex;justify-content:space-between;align-items:center;gap:20px;margin-bottom:24px}button{border:0;background:#126c5b;color:white;padding:11px 19px;border-radius:7px;cursor:pointer;font:inherit;font-weight:600}button:disabled{opacity:.5;cursor:not-allowed}.secondary{background:#edf3ef;color:#172f32}input,select{display:block;width:100%;padding:11px;border:1px solid #abbdb3;border-radius:6px;font:inherit;background:#fff}label{display:block;font-weight:600;margin:12px 0}small{display:block;color:#536a60}form>button{margin-top:20px}.check{display:flex;gap:10px;align-items:center}.check input{width:auto}.error{color:#a01c26}.badge{font-size:12px;text-transform:uppercase;letter-spacing:.06em;background:#e7f2e9;padding:5px 9px;border-radius:5px}.price{font-size:25px;font-weight:700}.actions,.pagination{display:flex;gap:12px;align-items:center;flex-wrap:wrap}.pagination{justify-content:center;margin:30px 0}.search{display:flex;gap:14px;align-items:center;margin:20px 0}summary{cursor:pointer;font-weight:700}footer{text-align:center;padding:30px;color:#536a60;font-size:13px}:focus-visible{outline:3px solid #dca134;outline-offset:3px}@media(max-width:600px){nav{gap:12px;flex-wrap:wrap}.heading{align-items:flex-start;flex-direction:column}main{padding:0 16px}h1{font-size:27px}.panel{padding:20px}}
```

## `web/src/test/AlertForm.test.tsx`

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AlertForm } from '../pages/AlertsPage';
import { api } from '../api/client';
vi.mock('../api/client', () => ({api: {createAlert: vi.fn(), updateAlert: vi.fn()}}));
it('blocks missing location without calling the API', async () => {
  render(<QueryClientProvider client={new QueryClient()}><AlertForm done={() => {}}/></QueryClientProvider>);
  await userEvent.click(screen.getByRole('button', {name:'Save alert'}));
  expect(await screen.findByRole('alert')).toHaveTextContent('Enter a suburb or postcode.');
  expect(api.createAlert).not.toHaveBeenCalled();
});
it('submits a valid postcode and turns optional blank fields into null', async () => {
  vi.mocked(api.createAlert).mockResolvedValue({id:'test', suburb:null, postcode:'0800', minPrice:300000, maxPrice:800000, minBedrooms:2, propertyType:null, active:true, activeSince:'2026-01-01T00:00:00Z'});
  render(<QueryClientProvider client={new QueryClient()}><AlertForm done={() => {}}/></QueryClientProvider>);
  await userEvent.type(screen.getByLabelText('Postcode'), '0800');
  await userEvent.click(screen.getByRole('button', {name:'Save alert'}));
  expect(api.createAlert).toHaveBeenCalledWith(expect.objectContaining({postcode:'0800', suburb:null, propertyType:null}));
});
```

## `web/src/test/forms.test.ts`

```typescript
import { alertSchema } from '../forms';
const valid = { suburb:'', postcode:'0800', minPrice:100, maxPrice:200, minBedrooms:2, propertyType:'unit', active:true };
it('keeps leading zero postcodes', () => expect(alertSchema.parse(valid).postcode).toBe('0800'));
it('rejects no location and inverted prices', () => {
  expect(alertSchema.safeParse({...valid, postcode:''}).success).toBe(false);
  expect(alertSchema.safeParse({...valid, maxPrice:99}).success).toBe(false);
});
```

## `web/src/test/setup.ts`

```typescript
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
afterEach(cleanup);
```

## `web/tsconfig.json`

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": [
      "ES2022",
      "DOM",
      "DOM.Iterable"
    ],
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "jsx": "react-jsx",
    "strict": true,
    "skipLibCheck": true,
    "esModuleInterop": true,
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "types": [
      "vite/client",
      "vitest/globals",
      "@testing-library/jest-dom"
    ]
  },
  "include": [
    "src",
    "vite.config.ts"
  ]
}
```

## `web/vite.config.ts`

```typescript
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
export default defineConfig({
  plugins: [react()],
  server: { proxy: { '/api': process.env.API_PROXY_TARGET ?? 'http://localhost:8080' } },
  test: { environment: 'jsdom', setupFiles: ['./src/test/setup.ts'], globals: true },
});
```
