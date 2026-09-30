# Verification report

Prepared 28 September 2026.

## Checks completed

- Restored the actual NuGet and npm dependencies.
- Built the complete .NET solution in Release mode: **0 warnings, 0 errors**.
- Ran **7 xUnit unit test cases** for matching rules and validation: passed.
- Ran **1 ASP.NET in-process contract smoke test**: passed. It verifies startup with test configuration, liveness, unauthorized access to a protected route, Swagger UI and the generated OpenAPI document.
- Generated the real EF Core initial migration, designer and snapshot using `dotnet ef`.
- Ran EF's pending-model-changes check: no model/migration drift.
- Started the API in contract-only mode, exported its live OpenAPI JSON and generated TypeScript types from it.
- Built the React production bundle, including strict TypeScript checking: passed.
- Ran ESLint: passed.
- Ran **4 Vitest / React Testing Library test cases**: passed.
- Ran C# whitespace formatting and its verification gate in folder mode: passed.
- `npm audit` reported **0 known vulnerabilities** for the final installed dependency set at check time. This is not a security guarantee; future advisories can change the result.

## Checks not completed in this environment

- **The three PostgreSQL Testcontainers integration tests were compiled but not executed.** Docker and its socket were unavailable. An attempt to start a temporary native PostgreSQL instance was blocked by the environment's inability to run it as a non-root user. No database integration pass is claimed.
- Docker Compose startup and the production container build were not run here.
- GitHub Actions was authored but not executed on GitHub.
- Azure resources were not created, charged or deployed.
- No full browser end-to-end test of the live database workflow was run.

The integration suite is included unchanged with its normal Testcontainers provisioning. It covers migrations, actual SQL matching, boundary cases, concurrent duplicate prevention, ownership, authentication, indexes, read status and edit/pause behavior. Run it on a Docker-enabled machine before treating the application as end-to-end verified:

```sh
dotnet test tests/Api.Tests
docker compose up --build
```

Then follow the manual demonstration in Phase 1 of `BUILD_GUIDE.md`.

## Deliberate limits

Fake listings only; no scraping or real email. Simple short-lived custom JWT sessions, no refresh/reset/verification flows. Full-history matching is suited to a small teaching dataset. Editing an alert starts a new future window. Notification uniqueness is per alert/listing pair. A listing POST replay creates a distinct listing. Optional Azure deployment is an outline and requires network, secret, migration and cost configuration.
