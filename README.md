# Digital Library Management System

.NET microservices capstone — 3 independent ASP.NET Core services, database-per-service, JWT auth across service boundaries.

## Run locally

Each service needs its own terminal:

```bash
cd UserService && dotnet run        # http://localhost:5001
cd CatalogService && dotnet run     # http://localhost:5002
cd ReservationService && dotnet run # http://localhost:5003
```

Swagger UI: `http://localhost:{port}/swagger`

Dev environment uses EF Core InMemory — data resets on restart. Seed data:
- 3 books (`CatalogService/Data/DataSeeder.cs`)
- 1 librarian account, `librarian@library.com` / `Librarian123!` (`UserService/Data/DataSeeder.cs`)

## Architecture

Three independent services, database-per-service pattern:

| Service | Port | DB Context |
|---|---|---|
| UserService | 5001 | `UserService/Data/UserServiceContext.cs` |
| CatalogService | 5002 | `CatalogService/Data/CatalogServiceContext.cs` |
| ReservationService | 5003 | `ReservationService/Data/ReservationServiceContext.cs` |

Inter-service HTTP clients: `ReservationService/Services/HttpClients/` (typed `HttpClient`, interfaces `IUserServiceClient` / `ICatalogServiceClient` for testability).

## Business rules

Implemented in `ReservationService/Controllers/ReservationsController.cs` and `ReservationService/Services/CascadeService.cs`:

- Max 5 active reservations/user
- Reservation expiry: 7 days
- Checkout period: 14 days
- Late fee: $1/day
- Waitlist claim window: 48 hours
- Waitlist eligibility (5-limit) checked at claim time, not join time

Waitlist cascade (return → skip increment → auto-reserve for next eligible patron → expire and cascade to next if ineligible → release to general availability if queue empty) lives entirely in `CascadeService.ReleaseOrCascadeAsync`, shared by:
- `ReservationsController.Return`
- `WaitlistController.Leave` (cancelling a Notified entry)
- `WaitlistExpiryJob` (hourly background job, `ReservationService/Services/BackgroundJobs/WaitlistExpiryJob.cs`)

## API endpoints

13 public endpoints across 3 services — full request/response contracts in `api-contracts.md`.

- `UserService/Controllers/AuthController.cs` — register, login
- `UserService/Controllers/UsersController.cs` — internal validate endpoint
- `CatalogService/Controllers/CatalogController.cs` — browse, search, detail, internal availability update
- `ReservationService/Controllers/ReservationsController.cs` — create, view active, checkout, return, history
- `ReservationService/Controllers/WaitlistController.cs` — join, view, leave

Rate limiting: 100 req/min per IP, fixed window, all 3 services (`Program.cs`, `AddRateLimiter`).

## Tests

```bash
dotnet test
```

28 tests across 3 projects (`*.Tests`), `WebApplicationFactory` integration style. `ReservationService.Tests` mocks `IUserServiceClient` / `ICatalogServiceClient` (`ReservationService.Tests/Mocks/`) instead of hitting real HTTP.

Coverage (excludes generated/framework code — see `coverlet.runsettings`):

```bash
dotnet test --collect:"XPlat Code Coverage" --settings:coverlet.runsettings
reportgenerator -reports:"*.Tests/TestResults/*/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"TextSummary"
cat CoverageReport/Summary.txt
```

84.2% line coverage, 89.5% method coverage as of last run.

## Migrations

EF Core migrations generated for all 3 services (`*/Migrations/`), applied automatically via `context.Database.Migrate()` on non-Development startup (`Program.cs`). To regenerate after a schema change:

```bash
cd <Service>
ASPNETCORE_ENVIRONMENT=Production dotnet ef migrations add <MigrationName>
```

(`ASPNETCORE_ENVIRONMENT=Production` is required — Development resolves to the InMemory provider, which has no migrator.)

## CI

`.github/workflows/ci-cd.yml` - runs on every push/PR to `main`, plus manual trigger (`workflow_dispatch`).

- **unit-tests** - `dotnet build` + `dotnet test` across all 3 test projects
- **endpoint-behavior-tests** - runs after `unit-tests` passes. Builds all 3 services (`Release`), starts them (`run-all.sh`), waits on `/health` (60s timeout, dumps logs on failure), then runs `endpoint-behaviors/smoke-test.sh` and `endpoint-behaviors/cascade-test.sh` against the live stack — full register → reserve → checkout → return → waitlist cascade flow with real HTTP calls between services

Local run:
```bash
chmod +x run-all.sh run-endpoint-tests.sh endpoint-behaviors/*.sh
./run-endpoint-tests.sh
```