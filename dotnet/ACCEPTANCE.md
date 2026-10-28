# Core rewrite acceptance inventory

Checkpoint 16 on `rewrite/dotnet-core-journeys` adds page-specific
learning notes, the consolidated log and confidence-check lifecycle records.
Git history supplies its commit hash. The
latest local Release suite passed **382 tests**. See [the AI log](AI-LOG.md) for
per-slice evidence and checkpoint history.
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
| A04 | Complete standard registration, including local authority, role, experience and explicit preferences | Partial | [RegistrationController](src/EarlyYearsFoundationRecovery.Web/Controllers/RegistrationController.cs), [domain tests](tests/EarlyYearsFoundationRecovery.UnitTests/UserAccountTests.cs) and [reference-data tests](tests/EarlyYearsFoundationRecovery.UnitTests/RegistrationReferenceDataTests.cs) exist. Controller journey tests submit each step through completion for standard and childminder settings, with reference validation and saved authority prefill. The [summary](src/EarlyYearsFoundationRecovery.Web/Views/Registration/CheckAnswers.cshtml) shows friendly labels, Yes/No preferences, explicit missing answers and labelled review links; rejected completion retains metadata and validation errors. Controller/actual-Razor and isolated HTTP coverage verify summary/review behaviour; live HTTP checks cover selected forms without changing learner data. Full Rails-equivalent browser/PostgreSQL acceptance remains outstanding. |
| A05 | Complete custom-setting/custom-role and conditional non-England/no-role paths | Partial | General roles include other with a required trimmed description, available without JavaScript; childminder roles reject general/custom choices. Account/registration choices share a helper. Controller journey tests complete custom-setting/custom-role, non-England, authority-without-role and no-role branches, and cover stale-field clearing, skipped-step POSTs and missing replacements preventing completion. Actual-Razor tests cover role form applicability and summaries that omit inapplicable answers/links and encode custom text. Combined custom fields differ from Rails' separate screens. Full browser/PostgreSQL acceptance and human approval of UX differences remain outstanding. |
| A06 | Save drafts, revisit steps and resume registration without losing answers or bypassing required fields | Partial | Each update persists and domain completion checks required fields. Summary review links retain identity-bound review mode across requests; valid edits collect first outstanding required answers or return to summary, and invalid input stays on the form. Controller tests cover newly required authority/role/experience, terms/preferences, normal-flow preservation, completed-account/identity isolation and review termination. [HTTP tests](tests/EarlyYearsFoundationRecovery.UnitTests/RegistrationReviewHttpTests.cs) use isolated in-memory accounts and real cookie TempData, form binding and anti-forgery for name, country and setting changes. PostgreSQL restart/resume, full direct-step validation sequencing and browser acceptance remain outstanding. |
| A07 | View and change account name, location, setting, role and contact/research preferences | Partial | [Account page](src/EarlyYearsFoundationRecovery.Web/Views/Account/Index.cshtml) shows name/email/registration, country/applicable authority, setting, role/experience and email/research preferences. Dedicated edits prefill values, validate and save back with confirmation; Cancel does not submit. Country/authority and role/experience use combined forms. [Setting edit](src/EarlyYearsFoundationRecovery.Web/Views/Account/Setting.cshtml) selects a setting without saving, then validates and saves all applicable dependent answers together; invalid inputs leave every saved answer untouched. These atomic account edits intentionally differ from Rails' separately persisted steps. Registration remains unchanged. Controller/actual-Razor tests cover reference/group validation, conditional clearing, single-save persistence, validation redisplay, independent preferences and guards; live HTTP checks cover no-save preview, invalid choices, anti-forgery and unchanged-value persistence. Human UX/accessibility review and full HTTP/PostgreSQL acceptance remain outstanding. |
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
| T01 | List released modules in order with appropriate available/in-progress/completed presentation | Partial | [Index](src/EarlyYearsFoundationRecovery.Web/Views/Training/Index.cshtml) separates state groups, orders modules by position within each group, and handles learner guidance and empty/completed states. Completed modules show recorded UK dates and certificate actions; in-progress modules expose latest graded-test actions. Named draft modules show non-linked upcoming cards with encoded CMS text; historical progress cannot override draft classification, and training routes stay blocked. Mapping/service/controller/actual-Razor tests cover these cases. Full Rails release classification remains outstanding: .NET still uses its existing minimal draft rule, not artwork/marketing, page-position and question-count integrity gates. Live checks cover eight working modules, not future cards because none are present in the current feed. |
| T02 | Overview shows module description, sections/topics, completion indicators and appropriate links | Implemented | [Overview](src/EarlyYearsFoundationRecovery.Web/Views/Training/Module.cshtml) groups section/topic boundaries using [overview models](src/EarlyYearsFoundationRecovery.Web/Models/TrainingOverviewSection.cs), hides pre-confidence/feedback sections and individual questions, and computes progress/link availability from saved visit names and assessment/completion state. Module description, outcomes and criteria use sanitized Markdown/rich text, with optional-field omission. Focused mapping/controller/Razor tests and live checks of all eight published overviews pass. Full visual/accessibility and PostgreSQL acceptance remain separate gates. |
| T03 | Resume normal learning; completed modules return to certificate; failed tests offer retake introduction | Implemented | [Journey tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingJourneyServiceTests.cs) and [Razor tests](tests/EarlyYearsFoundationRecovery.UnitTests/AssessmentSummaryRenderingTests.cs) cover state-aware CTAs and removed-page fallback. Verify alongside the new overview, including direct-URL visits. Do not infer direct-URL restrictions solely from disabled overview links. |
| T04 | Viewing overview/pre-learning pages does not prematurely record module start | Implemented | Overview no longer writes a start; page views retain resume data independently. The service records start only for `sub_module_intro`; existing timestamps remain unchanged. Domain/service/controller regressions cover pre-confidence submission, direct page visits, listing state, revisits and legacy starts. Automated PostgreSQL lifecycle evidence remains part of Q02. |
| T05 | Render supported content faithfully and safely, including feedback guidance and linked assets | Implemented | [Contentful mapping tests](tests/EarlyYearsFoundationRecovery.UnitTests/ContentfulTrainingContentProviderTests.cs) and [renderer tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingContentRendererTests.cs) cover rich text, assets, shortcodes and sanitization. Broader published-content coverage and unsupported rich-text layouts remain; see [supported limits](README.md). |
| T06 | Video embeds, fallback links and keyboard-operable transcripts work in supported browsers | Unverified | Mapping/URL validation tested and live transcript/fallback checked. Normal-browser playback remains unverified; integrated-browser Vimeo playback aborted. |
| T07 | Pre/post confidence answers restore, remain editable and advance without correctness feedback | Implemented | Controller/service and Razor coverage in [journey tests](tests/EarlyYearsFoundationRecovery.UnitTests/TrainingJourneyServiceTests.cs) and [rendering tests](tests/EarlyYearsFoundationRecovery.UnitTests/AssessmentSummaryRenderingTests.cs); published hints checked manually. Add a complete PostgreSQL-backed persisted pre/post journey. |
| T08 | Skip optional pre-confidence from its introduction and return without being forced into skipped questions | Implemented | Focused controller/Razor and service lifecycle coverage. Skipping submits no answers, records only a skipped timestamp (not completion), and does not unlock certificates. Live keyboard/mobile checks covered the route. |
| T09 | Record distinct pre/post confidence started/completed/skipped lifecycle state idempotently | Implemented | New `ConfidenceCheckProgress` entity and unique learner/module/type row record pre/post start/completion and optional pre-check skip. Page views and first learning start drive idempotent transitions; tests cover multi/single question, repeat views, skip, later start and independent check types. Add PostgreSQL restart/concurrency acceptance; reporting analytics remain deferred. |
| T10 | Deferred feedback does not masquerade as working forms or trap the core learning journey | Partial | Actual feedback workflows remain deferred. Published feedback pages now show an explicit unavailable/no-submission notice instead of CMS question content. Continue without submitting feedback skips only consecutive feedback pages to the next normal page; the end-of-module fallback returns to the overview. No form/answer is saved and existing completion/certificate guards remain unchanged. Actual-Razor/model/controller tests cover conditional continuation, no form, rejected feedback POSTs, recorded visit only and unchanged eligibility. Human UX and full browser/PostgreSQL acceptance remain outstanding. |

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
| N01 | Save/edit a note on a training notes page and continue to the correct next page | Implemented | CMS notes flags enable editors on text/topic-introduction pages. Notes use learner/module/page identity and retain the CMS title; POST navigation is server-derived. Service/controller/actual-Razor/HTTP tests cover separate notes, updates, clearing, 20,000-character boundary, encoded text, invalid destinations and anti-forgery. The additive EF migration preserves earlier module notes under an empty page name; their editor remains available. Local migration applied; live CMS/browser UX acceptance remains outstanding. |
| N02 | Show consolidated learning log grouped by active modules, including titles and empty states | Implemented | [Learning log](src/EarlyYearsFoundationRecovery.Web/Controllers/LearningLogController.cs) groups all notes for each published started module, including empty groups and retained legacy module notes. The navigation is hidden until a registered learner starts published training. Service tests cover ordering, empty groups, draft exclusion and user isolation; HTTP tests cover route, nav visibility and empty state; Razor tests cover encoding, dates and links. Human UX/accessibility and live Contentful acceptance remain outstanding. |
| N03 | Preserve notes across restarts; prevent cross-user edits; show save failures and validation | Unverified | [Stores](src/EarlyYearsFoundationRecovery.Infrastructure/Persistence/Stores.cs) scope by learner/module/page, backed by a unique index. In-memory service/HTTP tests cover repeat-save identity, separation, spoofed posted identity/title/navigation, validation and clearing; these do not establish PostgreSQL ownership/durability. Both note editors require completed registration. Add database/restart/concurrency/save-failure and account-closure evidence. |

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
   and completion eligibility is unchanged.
   Automated PostgreSQL lifecycle verification remains outstanding under Q02.
2. **Overview structure and availability (T01, T02, T03).** Establish required CMS
   metadata, then translate Rails section/topic and CTA cases. Link availability
   and direct-URL access are separate requirements. Section/topic structure and
   link rules and description/outcomes/criteria are implemented locally.
   Grouped course-list states, graded-test actions, completed dates/certificates
   and upcoming cards are implemented; full Rails release-integrity classification
   remains a separate gate.
3. **Completion and deferred-feedback boundary (S05, T10).** The explicit
   unavailable-feedback continuation is implemented locally, pending human UX and
   end-to-end acceptance. Completion timing/eligibility remains an explicit
   decision; preserve thank-you timing and the existing pass guard until agreed.
   Continue testing failed/passed/direct-entry/revisit cases.
4. **Account parity (A04-A09).** Translate registration branches and account editing
   into repeatable tests, add missing edit surfaces and consistent guards.
5. **Page-specific notes (N01-N03).** In-page editing, additive storage and the
   consolidated log are implemented. Next: database ownership/durability/closure.
6. **Confidence lifecycle (T07-T09).** Lifecycle state is implemented and
   migrated. Next: PostgreSQL-backed answer/lifecycle journey and concurrency.
7. **Integration and acceptance gates (A01-A03, Q01-Q07).** Add PostgreSQL/HTTP
   evidence as the earlier slices land, then exercise real One Login and deployment
   runtime and complete accessibility/operational review. These are not a final
   cosmetic pass and must not be postponed until after release.

## Decisions and exit criteria

- Preserve passing-assessment requirements while deciding completion timing.
  Rails behaviour and the current stricter .NET guard must be reconciled explicitly.
- Review the explicit unavailable-feedback continuation without implied submission.
- Route shape, combined registration screens, account return navigation and design
  system differences are not automatically approved merely because they exist.
- Rich-text support must cover required published content or reject unsupported
  content explicitly; no silent loss is acceptable.

Phase one can be accepted only when the scoped cases above are implemented or have
an explicitly approved boundary/difference, with the corresponding automated,
database, browser and integration evidence. Test totals alone are not acceptance.
The database import and production cutover remain separate later projects.
