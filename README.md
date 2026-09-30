# Aussie Alerts — C# / React / PostgreSQL

A full-stack learning app for **fictional Australian property listings only**. No scraping, real listing feeds, outbound email, or additional backend services.

## Start here

Install Docker Desktop (Linux containers) and Node.js 24. From this folder:

```sh
node scripts/setup.mjs
docker compose up --build
```

Open <http://localhost:5173>. Open the generated `.env` locally for the demo admin credentials; never commit it. Register a fictional user, create an alert for `0800` / AUD 400,000–600,000 / 2+ bedrooms / unit, then log in as admin in another browser window and add the prefilled fictional Darwin unit. Allow about twenty seconds for the inbox notification.

- [Step-by-step build guide](BUILD_GUIDE.md): 12 phases, checklists, commands, C# explanations and an 8-week schedule.
- [Complete source reading companion](FINAL_SOURCE.md): full authored files, without missing implementations.
- [Verification report](VERIFICATION.md): exactly what was and was not run.
- [Generated OpenAPI](openapi.json): contract used by the TypeScript client.

## Tools

.NET 10 SDK, Node.js 24, PostgreSQL 17, Docker Compose v2. `dotnet-tools.json` pins the EF command-line tool. C# package references and the npm lockfile pin dependencies. PostgreSQL is supplied by Compose/Testcontainers; you do not need a separate local PostgreSQL installation.

## Local API and React development

Start only PostgreSQL and run the API from source:

```sh
docker compose up -d postgres
dotnet tool restore
node scripts/run-api.mjs
```

In another terminal:

```sh
npm ci --prefix web
npm --prefix web run dev
```

Stop any Compose API/web instances before running their local equivalents on the same ports. `run-api.mjs` reads `.env`, configures local PostgreSQL, migrates, bootstraps the admin and starts the worker. It does not overwrite credentials for an admin already in the database.

## Verify

```sh
dotnet build AussieAlerts.slnx -m:1
dotnet test tests/Api.Tests
npm --prefix web run lint
npm --prefix web test
npm --prefix web run build
```

The full xUnit run requires Docker for PostgreSQL Testcontainers. To run only unit and API contract tests:

```sh
dotnet test tests/Api.Tests --filter "Category!=Integration"
```

## Regenerate the client

With the API running in Development:

```sh
node scripts/export-openapi.mjs
npm --prefix web run generate:api
```

For generation without PostgreSQL, start `node scripts/run-api.mjs --contract` instead. Commit both `openapi.json` and `web/src/api/schema.d.ts`. `openapi-typescript` generates types and `openapi-fetch` supplies the typed transport. Do not hand-edit the generated schema.

## Main rules

- Alerts match future listings. Editing/re-enabling resets the future window.
- If both suburb and postcode are specified, both must match.
- Prices are inclusive; bedrooms means a minimum.
- Overlapping alerts can each generate a notification for one listing.
- A unique `(AlertId, ListingId)` index and atomic PostgreSQL insert prevent duplicate notifications on retries.
- All simulated emails stay in PostgreSQL. JWTs stay in browser memory, so refresh requires login.
- Listing titles, prices and locations are fictional inputs, not verified property information.

## API and health

Development Swagger: <http://localhost:8080/swagger>. OpenAPI: <http://localhost:8080/openapi/v1.json>.

Liveness: `/health/live`. Database readiness: `/health/ready`.

Production image: `docker build --target production -t aussie-alerts:local .`. ASP.NET serves the built React application. See Phase 12 for cloud secrets, migrations, worker availability and cost notes.

## Stop

```sh
docker compose down
```

The PostgreSQL volume is preserved. `docker compose down -v` is a destructive reset that removes the local database. Do not use it to fix an ordinary application bug.
