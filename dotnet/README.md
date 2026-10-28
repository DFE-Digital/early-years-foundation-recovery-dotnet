# Early Years Foundation Recovery .NET application

This is a new implementation of the core user journeys from the Rails application on `main`. It is being designed from the existing behavior and tests, not ported from the older .NET experiments.

## Scope

The initial implementation targets Gov.UK One Login authentication, registration and account management, Contentful-backed training journeys, learning notes, assessments, progress and certificates. It uses a new PostgreSQL schema. Importing Rails users or progress, analytics and exports, bulk email/Notify workflows, feedback operations, and production Azure cutover are later work.

See the [core acceptance inventory](ACCEPTANCE.md) for source-backed journey status, evidence gaps, known Rails differences and the remaining implementation order. Working local journeys and passing focused tests do not yet establish full parity or production readiness.

The [AI-assisted development log](AI-LOG.md) records checkpoint history, decisions, validation and limitations. Its initial history was backfilled retrospectively; it is not a verbatim conversation transcript.

## Projects

- `src/EarlyYearsFoundationRecovery.Domain` — user, progress, notes, and assessment rules.
- `src/EarlyYearsFoundationRecovery.Application` — use cases and infrastructure contracts.
- `src/EarlyYearsFoundationRecovery.Infrastructure` — PostgreSQL persistence and external services.
- `src/EarlyYearsFoundationRecovery.Web` — ASP.NET Core MVC pages and authentication.
- `tests/EarlyYearsFoundationRecovery.UnitTests` — focused domain and application tests.

## Local setup

Training page, question and video transcript bodies render Markdown headings, lists, links, images and tables. Contentful rich-text documents are converted into that same sanitized Markdown pipeline, including headings, paragraphs, bold/italic/code marks, nested lists, blockquotes, HTTP(S)/root-relative links, and simple tables with a header row and single-line, single-paragraph cells. This also applies to module criteria used on certificates. Literal Markdown syntax in rich-text text nodes stays literal; raw HTML is disabled.

Embedded rich-text image assets resolve from the Contentful response, including localized file fields. Protocol-relative image URLs become HTTPS, the asset description supplies alternative text (empty descriptions produce decorative images), and training images scale down to fit their container. PNG, JPEG, GIF, WebP and AVIF assets are supported. Missing assets, invalid URLs, unsupported asset types, nodes, marks or table layouts raise explicit content errors instead of silently dropping content. Embedded entries, entry/asset hyperlinks, underline/superscript/subscript marks and complex rich-text tables remain unsupported; full Contentful parity is not yet claimed.

Rails `{two_thirds}` blocks render text beside their final image, stacking on narrow screens. Both spaced and compact blocks (without blank lines between text, image and markers) are supported. `{brain}`, `{book}` and `{info}` blocks render learning prompts headed "Reflection point", "Further reading" and "In your setting". `{quote}` blocks use their final separate paragraph as a citation. `{button}` wraps a Markdown link styled as a button; `{external}` wraps a link opening in a new tab with an explicit notice and `noopener noreferrer`.

`{inset-text}` blocks render inset guidance, including nested Markdown. `{details}` blocks render native, initially collapsed HTML details. Following the Rails `govuk_markdown` 2.0.3 convention, the first question mark or full stop separates the summary from the body: question marks remain in the summary, full stops do not. For example, `{details}Why does this matter? Further guidance.{/details}`. Details require a non-empty summary and body, support nested shortcodes, and open/close with the keyboard without JavaScript. Automated Chromium tests verify Enter and Space toggling at a mobile viewport.

The renderer transforms parsed Markdown rather than enabling raw CMS HTML; markers inside code or escaped literal text stay literal. Malformed or unmatched markers raise explicit content errors. Prompts can nest; nested two-thirds layouts are not supported. Prompt headings and styling are implemented without adding an icon-font dependency; the decorative Rails icons are not reproduced. More complex shortcode combinations and full content/accessibility parity still require verification.

YouTube and Vimeo video entries render responsive embeds with expandable transcripts. Invalid IDs and unsupported providers produce explicit content errors rather than arbitrary embed URLs.

Video pages include a direct player link if an embedded player is blocked. Live Vimeo embed metadata and transcript toggling have been checked locally, but playback in a normal browser still needs verification; the integrated browser aborted the external iframe request.

Pre- and post-module confidence questions use a Next button without marking opinions right or wrong. Saved selections are restored when revisiting a question and can be changed. Single-select questions reject multiple submitted options server-side.

Contentful text and topic-introduction pages with `notes: true` include an in-page learning-note editor. Save and continue creates or updates the current learner/module/page note and follows the next CMS page, or returns to the module when none follows. Empty submissions clear that page's text; notes are optional and limited to 20,000 characters. Validation redisplays submitted text without overwriting the saved note. Page titles and navigation come from CMS content, not posted fields. Other page types do not accept notes even if flagged.

The `/my-account/learning-log` page groups notes under every published module the learner has started, including empty module sections. It includes note dates, current-page links and the earlier module-level notes. The navigation link appears only after registration and starting a published module. The page-note migration preserves earlier module notes under an empty page name; their `/my-account/learning-log/{moduleName}` editor remains available from the aggregate and now requires completed registration, like training pages. Apply the migration before running the updated app. Automated HTTP tests use in-memory stores; separate PostgreSQL tests verify migrations, fresh-context persistence, learner isolation and account-closure note deletion. Process/database restarts, concurrent edits and save-failure acceptance remain outstanding.

Confidence questions display the Contentful `hint` as encoded plain text, and post-module confidence questions also display `description` through the sanitized Markdown/rich-text renderer, matching Rails. Radio options reference the displayed guidance for screen readers. Missing or whitespace-only guidance is omitted; factual questions do not display these confidence-only fields.

Pre- and post-module confidence checks now have a separate, idempotent learner/module/check record. Viewing the first question records the start; viewing the last records completion (a single-question check starts on first view and completes on a later view). If the learner reaches their first `sub_module_intro` without starting/completing a pre-check, its skip timestamp is recorded. Later revisits do not replace those timestamps, and a learner can still start a previously skipped check. These records are lifecycle state, not the deferred analytics/reporting pipeline.

The introduction immediately before pre-module confidence questions offers "Skip questions" to the next published `sub_module_intro`, matching the Rails optional entry route. It does not submit answers or mark the questions complete. The learning introduction's Previous link returns across the pre-confidence questions to their introduction; Previous within the questions still steps through them normally. No skip link appears on questions, interruption pages, post-module confidence checks, or when no learning introduction exists.

The module overview's Continue button resumes from the last visited page, matching the Rails milestone behaviour. New modules, or modules whose saved page was removed from Contentful, start at their first available page. Completed modules instead offer "View certificate", even after revisiting earlier content. A failed assessment offers "Retake test" via the published assessment introduction (or first summative question if no introduction exists), consistently on the overview and results page. An active retry resumes normally; passing a test without recorded module completion does not unlock the certificate button.

Opening an overview does not start a module. Page views and pre-confidence answers retain visited-page/resume information without setting the learning start date; reaching a `sub_module_intro` records that date once. Existing start dates are preserved, including records created by the earlier implementation. Direct visits to other page types do not implicitly start learning. Completion rules are unchanged and remain a separate parity gap.

My modules separates modules into in-progress, available, upcoming and completed groups, ordered by content position within each group. New and returning learners receive different guidance when no modules are in progress. Completing every listed module shows a completion message instead of empty learning groups; no published content retains an explicit empty message. Available/in-progress cards link to module overviews and show recorded page counts. In-progress modules with a graded latest test offer either Retake end of module test (failed) or View previous test result (passed); ungraded tests do not expose these actions. Retake links use the test introduction, falling back to the first test question if no introduction exists, consistent with the overview. Completed modules use a table with overview links, recorded completion dates and certificate actions. Dates use the same UK local-date conversion as certificates, including British Summer Time; listing modules does not write progress.

Registered learners can change their certificate name from My account through `/account/name`, with prefilled fields, existing required/100-character validation, Save and Cancel. Saving returns to My account with confirmation rather than sending the learner through registration. This does not change GOV.UK One Login details. The registration name step retains its Continue-to-location flow.

My account also displays email and research preferences with separate edit forms at `/account/email-preferences` and `/account/research-preferences`. Each prefills its saved yes/no answer, requires an explicit choice, saves only that preference while preserving the other, and returns with confirmation. Cancel does not submit. Both require a registered current account and anti-forgery protection; registration's combined preferences step is unchanged.

Country and applicable local authority are shown on My account and edited together at `/account/location`. Both selections use the existing registration reference data, with saved names mapped to choice IDs for prefill. The saved setting determines whether an English local authority is required; posted flags cannot override that requirement. Invalid choices do not change either saved value. A non-English country or a setting that does not require an authority clears it on save, while setting/role details and registration completion are preserved. Save returns to My account with confirmation; Cancel does not submit. This intentionally combines country and authority in one account-edit save rather than following Rails' multi-step setting-edit redirect, preventing a partially updated registered account. Registration's location journey remains unchanged.

Role and experience appear on My account and save together at `/account/role`, using roles for the persisted setting's role group and existing experience choices. This combined account form is a documented difference from Rails' separate role/experience steps. The general role group includes an explicit `other` choice with a required description (up to 200 characters); the description input is available without JavaScript. Choosing a standard role clears old custom text. Invalid selections change neither value, and successful updates use one store save without changing setting, location or registration completion. No-role settings show Not applicable and cannot submit role edits. Save returns with confirmation; Cancel does not submit. Registration remains unchanged. Human review of the combined account forms remains outstanding.

My account displays the setting title (or encoded custom description) and links to `/account/setting`. Choose a setting, then review and complete its applicable local-authority, role and experience answers before selecting Save. Continue does not persist anything; an unchanged setting prefills its saved answers, while a different setting requires fresh answers. The server derives requirements from the selected reference setting and saved country, not posted flags or choice lists, and validates every required answer before one store save. Non-applicable authority, role, experience and custom descriptions are cleared; country, preferences, name and registration completion are preserved. Other settings require a description of up to 200 characters. Cancel or Choose a different setting makes no changes. This atomic account-edit flow intentionally differs from Rails' separately persisted steps and still needs human UX acceptance. Registration is unchanged.

Registration now shares account editing's role-choice rules: general settings offer `other` with a required, trimmed description (up to 200 characters), while childminder settings retain only their reference roles. The general role description is available without JavaScript, even before selecting other. This preserves the existing combined .NET role/custom-description screen rather than adding Rails' separate custom-role step. Revisiting local authority maps the saved name back to its option ID. Both GET and POST skip that step when the persisted country/setting does not require it. Controller tests cover completion through standard, custom, childminder, non-English and no-role branches, invalid selections, stale-field clearing and incomplete replacement answers; full browser/PostgreSQL registration acceptance remains outstanding.

Check your answers displays the setting title/custom description and friendly experience label, falling back to the saved value for legacy reference IDs. Preferences display Yes/No, while missing answers are explicitly marked Not answered. Authority appears only when required in England; role and experience are omitted for no-role settings. Each displayed answer has a labelled Change link to its registration step, including authority and research preferences (both preferences still use the existing combined form). Custom text remains HTML-encoded. Rejected completion reloads the same display labels and preserves the validation error.

Summary Change links enable review mode for unfinished registrations. After a successful edit, the next required unanswered step is collected (including authority, role and experience newly required by a country/setting change), otherwise the learner returns straight to Check your answers. Validation errors preserve review mode; arriving at the summary or completing registration ends it. The review marker is tied to the authenticated subject using the existing protected cookie TempData provider, so it survives intervening requests without adding server session storage. It is not a return URL and cannot redirect outside the defined registration actions. Completed accounts and other identities ignore it. Normal registration keeps its existing forward sequence, and review does not itself complete registration. Controller and isolated HTTP tests cover conditional review paths, cookie lifecycle and anti-forgery; database restart/resume, full browser journeys and human UX acceptance remain outstanding.

Named modules currently classified as draft appear under Future modules in this course, with a Coming soon heading and optional Contentful `upcoming` text. They have no training links, page counts or assessment/certificate actions; direct training routes remain blocked. Historical progress is preserved but cannot turn a draft into an active or completed listing. Future modules prevent the all-completed message; when no currently available modules remain, an explicit empty available-group message is shown. Upcoming text is encoded plain text, matching Rails' paragraph presentation. This uses the existing draft rule (missing name/title or no resolved named pages), not Rails' full content-integrity validator: artwork/marketing requirements, page-position checks and exact question-count release gates still require a separately agreed audit/implementation. Missing overview metadata remains optional and existing working modules are not newly hidden.

Module overviews group content into learning sections, summary/next steps and completion, with topic-level progress calculated from persisted visited-page names. Interruption pages, pre-confidence sections and feedback sections are omitted from this outline; individual reading pages and questions are not listed. Incomplete topics remain text, completed topics link to their headers, and recorded module completion makes topics revisitable without requiring individual historical page records. A failed assessment is labelled Failed and does not expose a reflection link; passing makes reflection available. These are overview link rules, not new restrictions on direct page URLs.

Overviews display Contentful module `description`, `outcomes` and `criteria` through the same sanitized Markdown/rich-text renderer as training content. Outcomes use the Rails introduction "On completion of this module you will be able to:" and criteria appear under "This module covers:". Missing or whitespace-only metadata is omitted without empty headings or changing module availability. Grouped course-list presentation remains outstanding.

Formative answers become read-only after submission. Summative questions save and advance without revealing individual correctness during the test; results appear when all questions are answered. Passed assessments cannot be retaken or changed. Failed assessments can be retaken.

Learning checks redirect successful submissions to the saved question's GET page, so refreshing does not resubmit the answer. This page shows the original selection, locked options, and the question's Contentful `success_message` or `failure_message`, both immediately after submission and on later visits. Incorrect answers also show all correct option labels and, when guidance exists, an explanation through the sanitized Markdown/rich-text renderer. Invalid submissions remain on the form with validation errors and do not save an answer. Unanswered questions, confidence checks and unfinished summative questions do not show this feedback; the saved formative answer remains locked.

Summative submissions redirect to a GET results page so refreshing results does not resubmit answers. Contentful assessment-results pages use that same summary, and unfinished attempts do not display a final score.

Completed assessment results show only incorrectly answered questions, the learner's selected answer labels, and the question's Contentful `failure_message` guidance. Guidance uses the same sanitized Markdown/rich-text renderer and can link back to relevant topics. This review appears for failed attempts and passes with mistakes, but not perfect scores or unfinished attempts. It is read-only and does not expose unselected options or alter assessment answers. If an option has since been removed from Contentful, the review explicitly says it is no longer available. Razor rendering tests cover the actual results HTML.

Feedback collection remains deferred. Published `feedback` pages show a neutral Module feedback heading and explicitly state that collection is unavailable and no feedback has been submitted, rather than displaying an unanswered CMS question as ordinary reading content. Continue without submitting feedback links to the first following non-feedback page, skipping only consecutive feedback pages; if none exists, Return to module is shown. No feedback form or answer is saved, and these links do not bypass normal completion/certificate guards. Opening the boundary still records the normal page visit/resume state; skipped pages are not marked visited. Full feedback workflows and human UX acceptance remain outstanding.

Direct navigation to the thank-you page does not mark a module complete without a passing assessment. Completion timing is still thank-you rather than Rails' certificate access; changing that timing remains an explicit decision, not part of the feedback boundary. Certificate pages and PDF downloads require recorded module completion. Certificates include the current account name, module title, Contentful learning criteria, completion date in UK local time, and the Department for Education logo. Downloads are generated locally with Playwright/Chromium, with JavaScript and external network requests disabled, and are served with no-store caching. Full completion-flow parity remains unfinished.

### Certificate PDF runtime

After building the web project, install the matching Playwright Chromium runtime once per machine (and again when updating Playwright). With PowerShell available:

```sh
dotnet build dotnet/src/EarlyYearsFoundationRecovery.Web
pwsh dotnet/src/EarlyYearsFoundationRecovery.Web/bin/Debug/net10.0/playwright.ps1 install chromium
```

Alternatively, on macOS without PowerShell:

```sh
cd dotnet/src/EarlyYearsFoundationRecovery.Web/bin/Debug/net10.0
dotnet exec \
  --runtimeconfig EarlyYearsFoundationRecovery.Web.runtimeconfig.json \
  --depsfile EarlyYearsFoundationRecovery.Web.deps.json \
  Microsoft.Playwright.dll install chromium
```

Run installation as the same user that runs the application. Linux deployments need Chromium's system libraries too (`install --with-deps chromium`). CI installs these before running PDF tests. Certificate PDF tests open the generated PDF to verify its text, A4 dimensions and page count. No external PDF service is used; generated files are returned directly without saving personal certificates to disk. The PDF endpoint is `/training/{moduleName}/certificate.pdf`.

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

### PostgreSQL integration tests

Set `EYFR_TEST_POSTGRES` to a PostgreSQL connection string for a test-server role
with permission to create databases. Each test creates a uniquely named
`eyfr_integration_...` database, applies real migrations and drops only that
database during cleanup. The configured database is used solely as the
administrative connection; tests never apply migrations to it. Do not use
production credentials or servers.

```sh
EYFR_TEST_POSTGRES='Host=localhost;Database=postgres;Username=your_local_role' \
  dotnet test dotnet/EarlyYearsFoundationRecovery.slnx --configuration Release
```

Without this variable the eight PostgreSQL cases are explicitly reported as
skipped; other tests run normally. CI configures a PostgreSQL 17 service and
enables these tests. They cover upgrading existing notes, fresh-context
reads/edits, learner isolation, confidence answers/lifecycle, account-closure
note deletion, formative locking, interrupted summative attempts with multi-select
answer replacement, failed retries, passed-attempt locking and idempotent module
completion. Passing alone does not complete the module, and incomplete or failed
attempts cannot complete it.

The HTTP integration case runs the real MVC app against its isolated database,
with fixed training content and registration options. It uses Development
sign-in, real authentication cookies, anti-forgery tokens and Razor forms to
complete England/childminder registration, revisit/edit pre/post confidence
answers without correctness feedback, save a page note, validate and lock a
formative answer with refresh-safe explanation, fail an assessment,
sign out/in and resume, pass a retry, complete the module and download a real
Chromium PDF. It also checks account-name editing and account closure/redaction,
note deletion and sign-out. It requires the Chromium installation described above.
Neither live Contentful nor the shared development database is used.
These tests do not simulate database/app restarts, concurrent writes, real
One Login or a browser-driven accessibility journey.

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
