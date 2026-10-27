# Core rewrite acceptance inventory

Reviewed: 27 October 2026. Runtime baseline: `1dbb8b61` on
`rewrite/dotnet-core-journeys`. The latest local Release suite passed 173 tests.
This inventory is not a production-readiness declaration or evidence that CI ran.

## Scope and evidence

The target is the core learner journeys in Rails, not its implementation structure
or every Rails route. Production data import, identity continuity, production
cutover, reporting, bulk messaging, feedback workflows and non-core public/CMS
tooling remain deferred.

Status definitions:

- **Implemented**: current code has the behaviour and focused automated evidence.
  This does not imply database, browser, integration or accessibility acceptance.
- **Partial**: some behaviour exists, but an identified parity gap remains.
- **Unverified**: implementation exists, but the stated acceptance evidence is missing.
- **Missing**: the scoped capability is not implemented.
- **Deferred**: outside this delivery; the boundary still needs to be usable.

Test references below are repository-local evidence, not a claim that the Rails
suite was executed against .NET. Current controller tests use in-memory stores;
Razor tests render actual views; HTTP smoke tests cover only selected routes.
Manual browser checks are supplementary, not a repeatable end-to-end suite.

## Accounts and registration

Rails references:
[registration](../spec/system/registration_spec.rb),
[account details](../spec/system/account_page_spec.rb),
[account closure](../spec/system/closing_account_spec.rb),
[One Login callback](../spec/controllers/users/omniauth_callbacks_controller_spec.rb).

| ID | Acceptance case | Status | Current evidence and remaining work |
|---|---|---|---|
| A01 | Sign in with One Login; identify the learner; sign out locally and at the provider | Unverified | OIDC/PKCE and private-key JWT are wired in [startup](src/EarlyYearsFoundationRecovery.Web/Program.cs); [assertion tests](tests/EarlyYearsFoundationRecovery.UnitTests/GovOneClientAssertionTests.cs) cover the JWT, not a real provider exchange. Exercise integration-environment sign-in, callback, user info and logout. |
| A02 | Handle cancelled/failed sign-in and expired sessions with a usable recovery journey | Unverified | Framework authentication/session configuration exists. Add HTTP/integration cases for provider errors, missing claims and session expiry; inspect user-facing recovery. |
| A03 | Development sign-in is unavailable outside Development | Unverified | Environment guard exists in [AccountController](src/EarlyYearsFoundationRecovery.Web/Controllers/AccountController.cs); [smoke tests](tests/EarlyYearsFoundationRecovery.UnitTests/WebJourneySmokeTests.cs) verify only the development page. Add non-development HTTP coverage. |
| A04 | Complete standard registration, including local authority, role, experience and explicit preferences | Partial | [RegistrationController](src/EarlyYearsFoundationRecovery.Web/Controllers/RegistrationController.cs), [domain tests](tests/EarlyYearsFoundationRecovery.UnitTests/UserAccountTests.cs) and [reference-data tests](tests/EarlyYearsFoundationRecovery.UnitTests/RegistrationReferenceDataTests.cs) exist. Translate the Rails standard-setting browser case, including invalid submissions and final confirmation. |
| A05 | Complete custom-setting/custom-role and conditional non-England/no-role paths | Partial | Conditional routes and custom fields exist; some screens are combined unlike Rails. Verify every branch, stale-field clearing and displayed answers; document/approve intentional UX differences. |
| A06 | Save drafts, revisit steps and resume registration without losing answers or bypassing required fields | Unverified | Each update persists and domain completion checks required fields. Add PostgreSQL restart/resume and direct-step navigation cases; verify terms and validation sequencing. |
| A07 | View and change account name, location, setting, role and contact/research preferences | Partial | [Account page](src/EarlyYearsFoundationRecovery.Web/Views/Account/Index.cshtml) shows name/email/registration and only a name Change link. Add missing details/edit entry points and correct return-to-account behaviour; editing currently follows registration redirects. |
| A08 | Close an account with reason, confirmation/cancel, redaction, note deletion and sign-out | Unverified | [AccountService](src/EarlyYearsFoundationRecovery.Application/Accounts/AccountService.cs) and controller implement closure; domain test covers idempotency. Add full HTTP/PostgreSQL cases for redaction, deletion, cancellation, repeat requests and subsequent sign-in. Deferred feedback storage must be considered when introduced. |
| A09 | Enforce learner identity, registration and ownership consistently across core routes | Partial | [training tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingJourneyServiceTests.cs) cover registration guards; selected anonymous routes have HTTP coverage. [LearningLogController](src/EarlyYearsFoundationRecovery.Web/Controllers/LearningLogController.cs) requires authentication but lacks the training registration guard. Add cross-user and all-route negative cases. |

## Training, progress and content

Rails references:
[module list](../spec/system/my_learning_spec.rb),
[overview content](../spec/system/module_overview_content_spec.rb),
[overview progress](../spec/system/module_overview_progress_spec.rb),
[overview rules](../app/decorators/module_overview_decorator.rb),
[page lifecycle](../app/controllers/training/pages_controller.rb),
[confidence](../spec/system/confidence_check_spec.rb).

| ID | Acceptance case | Status | Current evidence and remaining work |
|---|---|---|---|
| T01 | List released modules in order with appropriate available/in-progress/completed presentation | Partial | Real published modules load, state and ordering exist. [Index](src/EarlyYearsFoundationRecovery.Web/Views/Training/Index.cshtml) is a single list; Rails separates state groups and future releases. Resolve required release metadata and translate list cases. |
| T02 | Overview shows module description, sections/topics, completion indicators and appropriate links | Missing | Current [overview](src/EarlyYearsFoundationRecovery.Web/Views/Training/Module.cshtml) is a flat list of all pages. Implement section structure, topic states, hidden pre-confidence/feedback sections and Rails link availability rules. |
| T03 | Resume normal learning; completed modules return to certificate; failed tests offer retake introduction | Implemented | [Journey tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingJourneyServiceTests.cs) and [Razor tests](tests/EarlyYearsFoundationRecovery.UnitTests/AssessmentSummaryRenderingTests.cs) cover state-aware CTAs and removed-page fallback. Verify alongside the new overview, including direct-URL visits. Do not infer direct-URL restrictions solely from disabled overview links. |
| T04 | Viewing overview/pre-learning pages does not prematurely record module start | Implemented | Overview no longer writes a start; page views retain resume data independently. The service records start only for `sub_module_intro`; existing timestamps remain unchanged. Domain/service/controller regressions cover pre-confidence submission, direct page visits, listing state, revisits and legacy starts. Automated PostgreSQL lifecycle evidence remains part of Q02. |
| T05 | Render supported content faithfully and safely, including feedback guidance and linked assets | Implemented | [Contentful mapping tests](tests/EarlyYearsFoundationRecovery.UnitTests/ContentfulTrainingContentProviderTests.cs) and [renderer tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingContentRendererTests.cs) cover rich text, assets, shortcodes and sanitization. Broader published-content coverage and unsupported rich-text layouts remain; see [supported limits](README.md). |
| T06 | Video embeds, fallback links and keyboard-operable transcripts work in supported browsers | Unverified | Mapping/URL validation tested and live transcript/fallback checked. Normal-browser playback remains unverified; integrated-browser Vimeo playback aborted. |
| T07 | Pre/post confidence answers restore, remain editable and advance without correctness feedback | Implemented | Controller/service and Razor coverage in [journey tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingJourneyServiceTests.cs) and [rendering tests](tests/EarlyYearsFoundationRecovery.UnitTests/AssessmentSummaryRenderingTests.cs); published hints checked manually. Add a complete persisted pre/post journey. |
| T08 | Skip optional pre-confidence from its introduction and return without being forced into skipped questions | Implemented | Focused controller/Razor coverage and live keyboard/mobile checks. Skipping submits no answers, does not complete confidence and does not unlock certificates. |
| T09 | Record distinct pre/post confidence started/completed/skipped lifecycle state idempotently | Missing | Answers persist, but no equivalent of [ConfidenceCheckProgress](../app/models/confidence_check_progress.rb) exists. Define and test lifecycle separately from deferred reporting/analytics. |
| T10 | Deferred feedback does not masquerade as working forms or trap the core learning journey | Deferred | Feedback question submission is unsupported; published feedback pages remain in the flat sequence. Provide an explicit, tested boundary/continuation without claiming feedback was submitted. Do not invent completion semantics. |

## Assessments and certificates

Rails references:
[formative questions](../spec/system/formative_question_spec.rb),
[summative assessment](../spec/system/summative_assessment_spec.rb),
[completion journey](../spec/system/training_module_completion_spec.rb),
[completion timing](../app/controllers/training/pages_controller.rb).

| ID | Acceptance case | Status | Current evidence and remaining work |
|---|---|---|---|
| S01 | Validate published answer IDs and single/multi-select constraints before saving | Implemented | [Journey tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingJourneyServiceTests.cs) cover invalid/missing/multiple selections. Add real HTTP model-binding and antiforgery cases. |
| S02 | Lock the first formative answer; show relevant explanation/correct labels; refresh uses GET | Implemented | Controller/service and actual Razor coverage; manual POST/302/GET and repeated refresh verified. Database concurrency/multiple-tab evidence remains absent. |
| S03 | Save summative answers and advance without revealing correctness before final grading | Implemented | [Journey tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingJourneyServiceTests.cs) cover incomplete attempts and final grading; add persisted interrupted-attempt/resume coverage. |
| S04 | Apply pass threshold, preserve passed attempts, allow failed retries and show refresh-safe results/review | Implemented | [Grader tests](tests/EarlyYearsFoundationRecovery.UnitTests/AssessmentGraderTests.cs), journey tests and Razor results tests cover scoring/retries/locking/incorrect answers. Verify full retry journey with PostgreSQL. |
| S05 | Record completion at the agreed lifecycle point, idempotently, without allowing an unearned certificate | Partial | .NET completes at thank-you and requires a pass when summative questions exist; Rails records completion at certificate after start. Existing tests deliberately enforce the current guard. Resolve timing and eligibility explicitly before changing them; do not silently weaken the guard. |
| S06 | Render learner-specific certificate preview/PDF with stable completion date and no-store responses | Implemented | [Certificate tests](tests/EarlyYearsFoundationRecovery.UnitTests/CertificateRendererTests.cs) inspect actual Chromium PDFs, logo, content, A4/page count and dates; controller tests guard completion. Live published certificate checked. Deployment runtime and larger content/name cases need acceptance. |

## Learning notes

Rails references:
[notes controller](../app/controllers/training/notes_controller.rb),
[request cases](../spec/requests/notes_spec.rb),
[navigation cases](../spec/system/learning_log_spec.rb).

| ID | Acceptance case | Status | Current evidence and remaining work |
|---|---|---|---|
| N01 | Save/edit a note on a training notes page and continue to the correct next page | Partial | .NET has a separate per-module editor, not in-page notes. Rails identifies a note by learner/module/page and retains its title. Extend identity, persistence, view integration and navigation; preserve existing local module notes rather than silently discarding them. |
| N02 | Show consolidated learning log grouped by active modules, including titles and empty states | Missing | No aggregate log route/view exists. [LearningLogController](src/EarlyYearsFoundationRecovery.Web/Controllers/LearningLogController.cs) requires a module. Implement aggregation and Rails navigation visibility after training starts. |
| N03 | Preserve notes across restarts; prevent cross-user edits; show save failures and validation | Unverified | [Stores](src/EarlyYearsFoundationRecovery.Infrastructure/Persistence/Stores.cs) scope by user/module and save to PostgreSQL; editor limits content length. Add automated database/HTTP ownership, repeat-save, failure and account-closure cases alongside page-specific storage. |

## Cross-cutting acceptance gates

These are requirements for accepting the scoped rewrite, not claims about
vulnerabilities or completed audits.

| ID | Acceptance case | Status | Current evidence and remaining work |
|---|---|---|---|
| Q01 | Build/run independently and apply clean PostgreSQL schema | Implemented | Local app and initial migration exercised; [CI workflow](../.github/workflows/dotnet.yml) exists. No remote CI success is claimed: work has not been pushed. Verify a fresh developer setup and CI runner. |
| Q02 | Repeated and interrupted journeys preserve durable data | Unverified | Local database used manually; [EF model tests](tests/EarlyYearsFoundationRecovery.UnitTests/ApplicationDbContextModelTests.cs) inspect mappings, not database behaviour. Add isolated PostgreSQL integration tests for registration, answers, retries, notes, completion and restart. |
| Q03 | Full core journey is repeatable through HTTP/browser automation | Missing | [Smoke tests](tests/EarlyYearsFoundationRecovery.UnitTests/WebJourneySmokeTests.cs) cover selected pages/auth redirects, not registration-to-certificate. Create deterministic content fixtures and full pass/fail/resume/edit/close scenarios. Do not rely on mutable live CMS or the shared local learner for automated acceptance. |
| Q04 | Forms/navigation meet accessibility and GOV.UK interaction expectations | Partial | Semantic forms, guidance references, responsive rendering and targeted keyboard tests exist. CSS approximates GOV.UK rather than using official Frontend. Test focus/error summary, labels, contrast, keyboard and screen reader journeys; resolve design-system approach before sign-off. |
| Q05 | Authentication, authorization, CSRF, session, privacy and logging requirements are verified | Unverified | Framework controls and selected focused tests exist; safe CMS rendering is tested. Conduct a dedicated review and negative HTTP tests across routes. No security sign-off is claimed by this inventory. |
| Q06 | Integration failures and operational readiness are explicit and usable | Partial | Configuration validation, database health and generic error handling exist. Verify Contentful/One Login/database failures, missing content, correlation/logging and readiness in a deployment-like environment. |
| Q07 | Certificate Chromium runtime works within deployment constraints | Unverified | Local runtime works; CI installation is configured. Verify Linux fonts/dependencies, process resources, failure handling and simultaneous downloads in the intended environment. |

## Implementation order and dependencies

1. **Training lifecycle baseline (T04, T03): implemented locally.** Page views are
   separate from module start, resume information and old timestamps are preserved,
   and completion eligibility is unchanged. Latest local Release suite: 185 passed.
   Automated PostgreSQL lifecycle verification remains outstanding under Q02.
2. **Overview structure and availability (T01, T02, T03).** Establish required CMS
   metadata, then translate Rails section/topic and CTA cases. Link availability
   and direct-URL access are separate requirements.
3. **Completion and deferred-feedback boundary (S05, T10).** Agree certificate
   eligibility and the user-visible deferred-feedback route before implementation.
   Test failed/passed/direct-entry/revisit cases; retain the pass guard until an
   explicit decision says otherwise.
4. **Account parity (A04-A09).** Translate registration branches and account editing
   into repeatable tests, add missing edit surfaces and consistent guards.
5. **Page-specific notes (N01-N03).** Extend storage without losing existing module
   notes, wire training-page editing and consolidated log, then ownership/closure.
6. **Confidence lifecycle (T09).** Build on corrected module-start/completion rules;
   preserve editable answers and optional pre-check behaviour.
7. **Integration and acceptance gates (A01-A03, Q01-Q07).** Add PostgreSQL/HTTP
   evidence as the earlier slices land, then exercise real One Login and deployment
   runtime and complete accessibility/operational review. These are not a final
   cosmetic pass and must not be postponed until after release.

## Decisions and exit criteria

- Preserve passing-assessment requirements while deciding completion timing.
  Rails behaviour and the current stricter .NET guard must be reconciled explicitly.
- Agree how learners continue past deferred feedback without implied submission.
- Route shape, combined registration screens, account return navigation and design
  system differences are not automatically approved merely because they exist.
- Rich-text support must cover required published content or reject unsupported
  content explicitly; no silent loss is acceptable.

Phase one can be accepted only when the scoped cases above are implemented or have
an explicitly approved boundary/difference, with the corresponding automated,
database, browser and integration evidence. Test totals alone are not acceptance.
The database import and production cutover remain separate later projects.
