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

Training page, question and video transcript bodies render Markdown headings, lists, links and tables. Raw HTML is disabled and generated HTML is sanitized using an allowlist based on the Rails content helper. YouTube and Vimeo video entries render responsive embeds with expandable transcripts. Invalid IDs and unsupported providers produce explicit content errors rather than arbitrary embed URLs. Linked assets and Contentful rich-text fidelity still require further parity work.

Video pages include a direct player link if an embedded player is blocked. Live Vimeo embed metadata and transcript toggling have been checked locally, but playback in a normal browser still needs verification; the integrated browser aborted the external iframe request.

Pre- and post-module confidence questions use a Next button without marking opinions right or wrong. Saved selections are restored when revisiting a question and can be changed. Single-select questions reject multiple submitted options server-side.

The module overview's Continue button resumes from the last visited page, matching the Rails milestone behaviour. New modules, or modules whose saved page was removed from Contentful, start at their first available page.

Formative answers become read-only after submission. Summative questions save and advance without revealing individual correctness during the test; results appear when all questions are answered. Passed assessments cannot be retaken or changed. Failed assessments can be retaken.

Summative submissions redirect to a GET results page so refreshing results does not resubmit answers. Contentful assessment-results pages use that same summary, and unfinished attempts do not display a final score.

Direct navigation to the thank-you page does not mark a module complete without a passing assessment. Certificate pages require recorded module completion. Certificate PDF generation and full completion-flow parity remain unfinished.

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

### Local Contentful settings

The web project is configured for .NET user secrets, automatically loaded in Development. From the repository root:

```sh
cd dotnet/src/EarlyYearsFoundationRecovery.Web
dotnet user-secrets set "Contentful:Space" "YOUR_SPACE_ID"
dotnet user-secrets set "Contentful:Environment" "YOUR_ENVIRONMENT_ID"
```

In macOS's default zsh terminal, enter the token without putting it in shell history:

```zsh
read -s "contentful_token?Paste Contentful Delivery token: "; echo
dotnet user-secrets set "Contentful:DeliveryToken" "$contentful_token"
unset contentful_token
```

Restart the app after editing secrets. Environment variables and command-line arguments override user secrets. For unpublished content, set `Contentful:Preview` to `true` and use `Contentful:PreviewToken` instead of a delivery token. The former `appsettings.Local.json` file is no longer loaded.

Supply secrets only through environment variables or a local secret store; do not commit credentials or private keys. User secrets are stored outside the repository as plain text, not encrypted, and are for development only. Do not point this initial application at production data. The database migration/import strategy is intentionally deferred.
