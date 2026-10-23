# Early Years Foundation Recovery .NET application

This is a new implementation of the core user journeys from the Rails application on `main`. It is being designed from the existing behavior and tests, not ported from the older .NET experiments.

## Scope

The initial implementation targets Gov.UK One Login authentication, registration and account management, Contentful-backed training journeys, learning notes, assessments, progress and certificates. It uses a new PostgreSQL schema. Importing Rails users or progress, analytics and exports, bulk email/Notify workflows, feedback operations, and production Azure cutover are later work.

## Projects

- `src/EarlyYearsFoundationRecovery.Domain` — user, progress, notes, and assessment rules.
- `src/EarlyYearsFoundationRecovery.Application` — use cases and infrastructure contracts.
- `src/EarlyYearsFoundationRecovery.Infrastructure` — PostgreSQL persistence and external services.
- `src/EarlyYearsFoundationRecovery.Web` — ASP.NET Core MVC pages and authentication.
- `tests/EarlyYearsFoundationRecovery.UnitTests` — focused domain and application tests.

## Local setup

Install the .NET 10 SDK and PostgreSQL 15 or later. The Development profile uses a local PostgreSQL database named `eyfr_dotnet` and sample training content. Set `ConnectionStrings__Application` if your local connection differs.

```sh
dotnet restore dotnet/EarlyYearsFoundationRecovery.slnx
dotnet test dotnet/EarlyYearsFoundationRecovery.slnx
cd dotnet
dotnet tool restore
dotnet tool run dotnet-ef -- database update \
  --project src/EarlyYearsFoundationRecovery.Infrastructure \
  --startup-project src/EarlyYearsFoundationRecovery.Web \
  --context ApplicationDbContext
cd ..
dotnet run --project dotnet/src/EarlyYearsFoundationRecovery.Web
```

In Development, visit `/account/sign-in` and use the local test-user sign-in. Outside Development, configure Gov.UK One Login with `Authentication__GovOne__Authority`, `Authentication__GovOne__ClientId`, and `Authentication__GovOne__PrivateKeyPath`. Set `Contentful__Space` and either `Contentful__DeliveryToken` or (when `Contentful__Preview=true`) `Contentful__PreviewToken` to load real training and registration content.

Supply secrets only through environment variables or a local secret store; do not commit credentials or private keys. Do not point this initial application at production data. The database migration/import strategy is intentionally deferred.
