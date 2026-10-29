# .NET migration: handoff and remaining delivery plan

Updated: 29 October 2026. Owner is pausing work; this document is intended for a
different agent to resume without the original conversation.

## 1. Read this first

The goal is to replace the working Rails service with a maintainable .NET/C#
implementation, preserving required user and operational behaviour, not copying
Rails internals. The learner implementation is already substantial. Do not start
another rewrite, assume the old experimental branch is authoritative, or spend
unbounded time adding isolated acceptance tests instead of closing delivery gates.

Two milestones must stay separate:

1. **Core learner acceptance:** integrate real authentication/content, resolve
   known behavioural differences, verify browser/accessibility and reliability.
2. **Full .NET replacement:** migrate or explicitly retire all remaining Rails
   responsibilities, transfer existing data/identity, deploy and cut over safely.

Previously deferred capabilities are not automatically out of scope for milestone
2. Each needs implementation or an explicit owner-approved retirement decision.
Merging the implementation did not authorize production deployment.

## 2. Baseline and evidence

- Repository: `DFE-Digital/early-years-foundation-recovery-dotnet`, a fork.
- [PR #3](https://github.com/DFE-Digital/early-years-foundation-recovery-dotnet/pull/3)
  merged into `main` on 29 October 2026.
- Reviewed baseline: `a3b76595` (merge), including `438487f9` (CI replacement).
  Working tree was clean before this documentation task.
- Last verified full suite: **423 passed, none skipped**, including **25 isolated
  PostgreSQL cases**. This is historical evidence, not a new test run for this plan.
- [Linux GitHub CI](https://github.com/DFE-Digital/early-years-foundation-recovery-dotnet/actions/runs/40697158357)
  passed the same 423 tests, with PostgreSQL 17 and Chromium PDF generation.
- The application lives in [dotnet/](dotnet/). Rails remains in the repository.
- [Acceptance inventory](dotnet/ACCEPTANCE.md) provides detailed learner evidence;
  [README](dotnet/README.md) provides setup and content limits;
  [AI log](dotnet/AI-LOG.md) provides implementation history.
  Some older inventory rows contain stale "not pushed"/outstanding-evidence wording:
  use this baseline and the linked tests to reconcile them, not to repeat completed work.

### Implemented; preserve rather than rebuild

- .NET 10 MVC/Razor, Domain/Application/Infrastructure/Web separation, EF Core/Npgsql.
- Registration with conditional country/authority/setting/role branches, custom
  descriptions, draft/review resume and explicit preferences.
- Account name/location/setting/role/preferences edits, closure/redaction/note
  deletion, account-record-bound cookies and stale-session rejection.
- Grouped module lists/overviews, learning start versus page-view distinction,
  state-aware navigation/resume, confidence answers and lifecycle tracking.
- Locked formative answers; summative incomplete answers, retries, grading and
  passed-attempt locking.
- Page-specific notes/learning log; guarded completion and Chromium certificate PDFs.
- Contentful content/reference providers and sanitized Markdown/rich-text rendering,
  with explicit supported-content limits.
- OIDC/PKCE/private-key JWT wiring, local test sign-in, explicit configured One Login
  entry in Development, failed/cancelled callback recovery, session expiry/renewal.
- Real MVC/anti-forgery/cookie/PostgreSQL HTTP tests, selected browser keyboard checks,
  and sequential in-process host reconstruction with persisted test keys.

**Not established:** real One Login exchange, production acceptance, complete Rails
parity, comprehensive browser/accessibility/security sign-off, full process/DB
restart resilience, or a runnable .NET production deployment.

## 3. Resume safely

1. Inspect branch, status, recent commits and current CI before editing. Preserve
   user changes. Create a fresh feature branch from current `main` for implementation.
2. Read this plan and the relevant acceptance rows/tests. Inspect the corresponding
   Rails behaviour/specs before deciding what "parity" means.
3. Use a short bounded slice with observable exit criteria. Update this plan and
   the acceptance inventory when facts change; use the next numbered AI-log entry.
4. Do not push, merge, alter protection, generate signing keys, change real service
   credentials, stop shared containers or modify production infrastructure without
   the appropriate user authorization.
5. A user-requested "checkpoint" historically means a local commit, no push unless
   requested. Include the repository-required coauthor trailer.

### Local environment pitfalls

- macOS workspace was `/Users/dfeetenby/repos/early-years-foundation-recovery-dotnet-1`.
  Do not assume that path, processes or services still exist next week.
- Rails containers used ports **3000** (web), **4000** (One Login simulator) and
  **5432** (database). The existing Homebrew PostgreSQL 17 .NET database was moved
  to **5433** after the Rails container took 5432.
- VS Code's [development task](.vscode/tasks.json) points to database `eyfr_dotnet`
  on localhost:5433 with the current OS user. Do not replace it with the Rails DB
  or recreate the database: the saved learner profile/progress must survive.
- Web profile: HTTPS **7197**, HTTP **5058**. Check `/health` after startup.
  The task is not watch mode; restart after code changes. Stop only the verified PID.
- Inspect shared browser state before making edits. Prefer isolated acceptance
  data; do not complete/close/edit the owner's learner just to test.
- Never dump user-secrets, environment files, private keys or tokens into output.

### Established validation command

```sh
EYFR_TEST_POSTGRES='Host=localhost;Port=5433;Database=postgres;Username=YOUR_LOCAL_ROLE' \
  dotnet test dotnet/EarlyYearsFoundationRecovery.slnx \
  --configuration Release --no-restore --logger 'console;verbosity=minimal'
```

Adjust the connection for the actual environment; do not weaken database auth.
Integration fixtures create/drop only random `eyfr_integration_...` databases and
need CREATE DATABASE permission. Without the variable, those tests explicitly skip.
Use focused `--filter 'FullyQualifiedName~...'` first, full suite at a meaningful
gate. Restore dependencies if needed on a fresh machine. Chromium setup is shown
in the [CI workflow](.github/workflows/dotnet.yml). This is xUnit v2/VSTest.
Compare persisted timestamp baselines, not EF-tracked sub-microsecond timestamps:
PostgreSQL precision caused Linux-only failures that have already been fixed.

## 4. Prioritized workstreams and exit criteria

### P0. Real authentication integration (next recommended slice)

Start with [Program.cs](dotnet/src/EarlyYearsFoundationRecovery.Web/Program.cs),
[authentication code](dotnet/src/EarlyYearsFoundationRecovery.Web/Authentication/),
[AccountController](dotnet/src/EarlyYearsFoundationRecovery.Web/Controllers/AccountController.cs),
[local setup](dotnet/README.md#testing-real-one-login-locally) and
[Rails integration notes](GOV_ONE.md).

- Inspect the already-defined One Login simulator in
  [docker-compose.dev.yml](docker-compose.dev.yml). It currently registers Rails
  callback/logout URLs, not .NET. Plan a separate/additional client configuration
  without breaking the Rails simulator or exposing its development key.
- Verify discovery, browser authorization, PKCE, private-key assertion, token
  validation, user-info/claim mapping, account creation and returning-user identity.
  In particular, verify when email becomes available relative to `OnTokenValidated`;
  current code requires subject/email there. This is a question to test, not a
  confirmed defect or justification for accepting unvalidated claims.
- Exercise missing/mismatched claims, cancellation, provider/token/user-info errors,
  expiry, key rotation, local/provider logout and trusted callback configuration.
- Development explicitly offers both local and configured real One Login sign-in.
  Development logout is local-only. Non-Development logout calls the provider.
- Decide the signed-out landing: it currently returns to sign-in and immediately
  initiates a new challenge. Confirm provider-required logout parameters instead
  of assuming the simulated end-session routing proves acceptance.
- **Exit:** reproducible simulator round-trip and documented real integration-
  environment acceptance with approved client/callbacks. Simulator success is not
  real-provider sign-off. If credentials are unavailable, record the external
  blocker and proceed to P1 rather than inventing credentials.

### P1. Live content and explicit product decisions

- Audit released/draft/future modules against Rails
  [content integrity](app/services/content_integrity.rb) and actual CMS contracts.
  Current .NET draft classification is deliberately minimal, not full release
  integrity checking. Preserve valid modules while agreeing stronger gates.
- Exercise representative published content, rich-text/assets/shortcodes, videos,
  transcripts, registration choices and certificates. Implement required missing
  rendering or reject unsupported content explicitly; no silent content loss.
- Approve completion timing: .NET currently completes at thank-you, requiring a
  passed summative assessment where present. Do not move timing or weaken the
  guard without resolving the Rails difference.
- Approve combined account-edit screens, URL differences and feedback boundary.
  Feedback pages currently say unavailable and continue without fabricating answers.
- **Exit:** an approved behaviour/content matrix, required rendering coverage and
  identified redirects; normal-browser video playback verified.

### P2. Bounded learner browser, accessibility and resilience acceptance

- Add repeatable browser coverage for representative registration branches,
  learning/notes, interrupted assessment, failure/retry/pass, certificate and
  account editing/closure, using isolated data.
- Finish required form error recovery, keyboard focus, semantics, contrast,
  responsive and screen-reader testing. Name/preferences/location/role have targeted
  fixes already; this is not full accessibility sign-off. Agree official GOV.UK
  Frontend versus the current approximate styling.
- Exercise true OS-process/DB restarts, persisted data-protection keys, concurrent
  notes/submissions/completion, duplicated requests and database/CMS save failures.
  The host reconstruction test is in-process; do not describe it as a process restart.
- Use a dedicated security review and focused negative tests for identity/ownership,
  anti-forgery, callbacks, session/privacy, CMS rendering and webhooks as introduced.
- **Exit:** representative browser suite and human acceptance evidence; failures
  visible/actionable; concurrency rules explicit and tested; required fixes closed.

### P3. Inventory and implement remaining Rails capabilities

Before implementation, turn [Rails routes](config/routes.rb), controllers, jobs,
models, tasks and specs into a capability matrix: required / explicitly retired /
replaced externally. Agree owners and acceptance criteria. The five .NET controllers
do not establish coverage of all these Rails surfaces.

| Surface to review | Starting evidence | Required outcome |
|---|---|---|
| Public/CMS pages, about/expert/course pages, legal/accessibility/cookie/settings behaviour, old URLs | [routes](config/routes.rb), [controllers](app/controllers/), [views](app/views/) | Required pages, consent, content and inbound links work; redirects agreed |
| Module/course feedback and research responses, including guest paths | [FeedbackController](app/controllers/feedback_controller.rb), shared answering logic/specs | Real validation/persistence/navigation/reporting/retention, or approved retirement; replace unavailable placeholders when implemented |
| Notify transactional/reminder/new-module mail and bulk delivery | [jobs](app/jobs/), [mailers](app/mailers/), [Notify callback](app/controllers/notify_controller.rb) | Consent/eligibility, templates, durable scheduling, retry/idempotency and delivery status without duplicate sends |
| Content change/release webhooks and checks | [ReleaseController](app/controllers/release_controller.rb), [webhook base](app/controllers/webhook_controller.rb), [content changes](app/services/content_changes.rb) | Authenticated/idempotent ingestion, cache invalidation, integrity checks and downstream notifications |
| Dashboard, analytics, audit and exports/cloud uploads | [Dashboard](app/services/dashboard.rb), [DashboardJob](app/jobs/dashboard_job.rb), tracking/auditing concerns | Agreed data definitions, privacy and verified output/consumer compatibility |
| Worker scheduling and operational tooling | [jobs](app/jobs/), [lib tasks](lib/tasks/), Rails preview/debug/snippet routes | Required work runs durably in .NET or a documented external replacement; non-production tools remain gated |

This table identifies review targets, not a claim that every legacy feature should
be rebuilt unchanged. Inspect reporting schemas, retention and consumer contracts
before choosing a worker framework or adding packages.

**Dependency:** settle data/behaviour contracts before migration, but prototype
deployment and migration early; neither should wait until the final week.

### P4. Existing learner data and identity migration

- Inventory production Rails schema/data and any prior import tooling, including
  other branches, without assuming the earlier experimental importer is suitable.
  The current EF migrations create the .NET schema, not a Rails production import.
- Map One Login subjects/clients, registration/preferences/consent, closure/redaction,
  module identifiers/timestamps, confidence/formative/summative history, notes,
  certificates and required feedback/mail/analytics records.
- Never match accounts by email alone or assume provider subject continuity across
  clients. Agree an auditable identity-link strategy with the service owner.
- Build repeatable dry-run import/reconciliation and exception reporting, preserving
  historical completion dates and ownership. Decide schema changes from real data.
- Rehearse on approved sanitized copies; define freeze/delta handling and rollback.
  Avoid dual writes unless an explicit consistency design is approved.
- **Exit:** reconciled counts and sampled learner journeys, documented unresolved
  records, repeatable import duration, identity proof, restore and rollback rehearsal.

### P5. .NET runtime, observability and deployment

- Root [Dockerfile](Dockerfile), compose, worker/entrypoints and Azure/Terraform
  deployment assets are still Rails-oriented. Build a deliberate .NET deployment
  path; adding test CI did not create an app deployment.
- Package Chromium/fonts/native dependencies; verify PDF limits, concurrent downloads,
  timeouts and cleanup in the intended Linux host/container, not only test CI.
- Provide secret/key management and rotation, HTTPS/proxy/callback handling, durable
  shared data-protection keys, PostgreSQL permissions/pooling and controlled migrations.
- Add readiness/liveness, safe logs/traces/metrics, alerts, failure UX, backup/restore,
  resource/load testing and operational runbooks. Scope dependencies per environment.
- Deploy to isolated staging first and run real authentication/content/migration
  acceptance there. CI must not need production keys.
- **Exit:** repeatable .NET staging deployment and production promotion/rollback,
  measured runtime behaviour and operational ownership.

### P6. Cutover and retire Rails

- Require explicit owner acceptance of P0-P5 and any approved retirements.
- Rehearse database migration, identity continuity, certificate/history access,
  old URL redirects, jobs/notifications and external webhook/client changes.
- Schedule freeze/delta import, switch traffic, smoke tests, monitoring and rollback
  criteria. Prevent Rails and .NET both sending mail or processing the same webhook.
- Only after the agreed observation period remove unused Rails app/worker assets,
  gems/npm dependencies, deployment workflows, credentials and infrastructure.
  Preserve required records/audit history and update public/developer documentation.
- **Exit:** users and required operations run on .NET without a hidden Rails runtime
  dependency; production acceptance and recovery are signed off.

## 5. CI, repository rules and cautions

- [Automatic .NET CI](.github/workflows/dotnet.yml): all PRs, merge groups,
  pushes to `main`, manual dispatch. Full Release suite with PostgreSQL/Chromium.
- Legacy Ruby CI was removed. [Rails DEV deploy](.github/workflows/azure-deploy-dev.yml)
  is manual-only. Production/staging and label-opted-in review deploys were not
  replaced. Audit remaining Rails-specific checks/workflows when retiring Rails.
- Root README still describes Rails and its old CI badge; update it as part of the
  wider documentation transition, not as evidence that .NET deployment exists.
- Ruleset previously included update restrictions, merge queue and 70% coverage;
  admin bypass was configured and signature requirement removed by the owner.
  Inspect current settings rather than assuming this persists. Do not weaken rules
  to hide failures; current suite has no established 70% coverage report.
- Local signing was not configured by this work. Do not invent identity/signatures.

## 6. Suggested first session next week

1. Confirm baseline/clean status, current CI and database ports; run targeted
   validation only if needed to establish the environment.
2. Inspect simulator configuration and agree a safe .NET client/callback setup.
3. Complete one real browser OIDC round-trip against the simulator, capturing only
   non-sensitive outcomes. Fix integration defects with focused regression tests.
4. Record what still requires real One Login credentials/provider approval.
5. Start the remaining-capability matrix in P3 while awaiting external access.

Do not treat this plan as approval to deploy, migrate production data, retire
features or change unresolved product behaviour. Ask the owner at those boundaries.
