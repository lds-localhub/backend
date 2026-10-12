# LocalHub — Backend

Backend of **LocalHub**, a marketplace of at-home local services (group G14, LDS 2026/2027).
It is a single ASP.NET Core application that exposes a REST API shared by the web and
mobile frontends.

| | |
|---|---|
| Language / framework | C# 14 / ASP.NET Core 10 |
| Database | PostgreSQL 18, accessed with EF Core (Npgsql) |
| Tests | xUnit and `Microsoft.AspNetCore.Mvc.Testing` |
| Main branch | `master` (protected: changes only enter through a Pull Request) |

## Prerequisites

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.0.401 or a later 10.0.x (pinned in `global.json`) | `dotnet --list-sdks` |
| PostgreSQL | 18 | `psql --version` |
| Git | any recent version | `git --version` |

Recommended IDE: JetBrains Rider. Database client: pgAdmin 4.

## Solution structure

```
LocalHub.slnx
├── src/
│   ├── LocalHub.Api             REST endpoints, authentication, composition root (Program.cs)
│   ├── LocalHub.Application     use cases (services)
│   ├── LocalHub.Infrastructure  EF Core / PostgreSQL and external systems
│   └── LocalHub.Domain          entities and business rules
└── tests/
    ├── LocalHub.UnitTests
    └── LocalHub.IntegrationTests
```

Dependencies between projects (they must never point the other way, and `Domain`
references nothing):

```
Api  →  Application  →  Infrastructure  →  Domain
```

Each layer registers its own services in a `DependencyInjection.cs` file
(`AddApplication` calls `AddInfrastructure`); `Program.cs` only calls `AddApplication`.

## Database setup

1. Run once, as the PostgreSQL administrator (for example in pgAdmin's Query Tool, connected as
   `postgres`), to create the application user with a password of your choice:

   ```sql
   CREATE ROLE localhub WITH LOGIN PASSWORD 'choose-a-password';
   ```

2. Create the `localhub_dev` database, owned by `localhub`, with the schema and the fictitious
   reference data (scripts and reference accounts in [`database/README.md`](database/README.md)).
   From the repository root (`psql` is in `C:\Program Files\PostgreSQL\18\bin` on Windows):

   ```powershell
   psql -U postgres -d postgres -f database/criar-base-dados.sql   # first time
   psql -U postgres -d postgres -f database/repor-base-dados.sql   # reset to the reference data
   ```

Use only **fictitious data** in every environment.

## Configuration

Secrets are never committed. The real configuration file is ignored by Git.

1. Copy `src/LocalHub.Api/appsettings.Development.example.json` to
   `src/LocalHub.Api/appsettings.Development.json`.
2. In the copy, replace `CHANGE_ME` with the password you chose above.

Alternatively, set the connection string through an environment variable (PowerShell):

```powershell
$env:ConnectionStrings__LocalHub = "Host=localhost;Port=5432;Database=localhub_dev;Username=localhub;Password=<password>"
```

## Build, test and format

Run from the repository root:

```powershell
dotnet build                          # must finish with 0 warnings and 0 errors
dotnet test                           # runs the unit and integration tests
dotnet format --verify-no-changes     # must report no changes
```

The integration tests do not need a database.

## Run

```powershell
dotnet run --project src/LocalHub.Api --launch-profile http
```

The API listens on `http://localhost:5041` (HTTP only, by design).

To check that the application can reach the database, open
`http://localhost:5041/api/health`: it answers `Healthy` when the connection works and
`Unhealthy` (HTTP 503) when it does not.

In the Development environment the OpenAPI document is served at
`http://localhost:5041/openapi/v1.json`.

## Working on the project

Rules from the group's internal regulation (RI v02, section 9):

- Never push to `master`. Create a branch per Issue: `<type>/i<number>-<short-description>`,
  for example `feature/i036-estrutura-base`.
- Commit messages: `<type>: <short-description> [I<number>]`, with type one of
  `feat`, `fix`, `test`, `docs`, `refactor`, `chore`, `wip`.
- Open a Pull Request with the Issue link, a summary, how to test, the acceptance criteria
  and the tests run. The Executor cannot approve or merge their own Pull Request; the merge
  is a squash merge done by the Reviewer after the Auditor's authorization.
- Before opening a Pull Request, `dotnet build`, `dotnet test` and
  `dotnet format --verify-no-changes` must all pass.
