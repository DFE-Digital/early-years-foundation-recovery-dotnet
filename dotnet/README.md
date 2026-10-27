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

Training page, question and video transcript bodies render Markdown headings, lists, links, images and tables. Contentful rich-text documents are converted into that same sanitized Markdown pipeline, including headings, paragraphs, bold/italic/code marks, nested lists, blockquotes, HTTP(S)/root-relative links, and simple tables with a header row and single-line, single-paragraph cells. This also applies to module criteria used on certificates. Literal Markdown syntax in rich-text text nodes stays literal; raw HTML is disabled.

Embedded rich-text image assets resolve from the Contentful response, including localized file fields. Protocol-relative image URLs become HTTPS, the asset description supplies alternative text (empty descriptions produce decorative images), and training images scale down to fit their container. PNG, JPEG, GIF, WebP and AVIF assets are supported. Missing assets, invalid URLs, unsupported asset types, nodes, marks or table layouts raise explicit content errors instead of silently dropping content. Embedded entries, entry/asset hyperlinks, underline/superscript/subscript marks and complex rich-text tables remain unsupported; full Contentful parity is not yet claimed.

Rails `{two_thirds}` blocks render text beside their final image, stacking on narrow screens. Both spaced and compact blocks (without blank lines between text, image and markers) are supported. `{brain}`, `{book}` and `{info}` blocks render learning prompts headed "Reflection point", "Further reading" and "In your setting". `{quote}` blocks use their final separate paragraph as a citation. `{button}` wraps a Markdown link styled as a button; `{external}` wraps a link opening in a new tab with an explicit notice and `noopener noreferrer`.

`{inset-text}` blocks render inset guidance, including nested Markdown. `{details}` blocks render native, initially collapsed HTML details. Following the Rails `govuk_markdown` 2.0.3 convention, the first question mark or full stop separates the summary from the body: question marks remain in the summary, full stops do not. For example, `{details}Why does this matter? Further guidance.{/details}`. Details require a non-empty summary and body, support nested shortcodes, and open/close with the keyboard without JavaScript. Automated Chromium tests verify Enter and Space toggling at a mobile viewport.

The renderer transforms parsed Markdown rather than enabling raw CMS HTML; markers inside code or escaped literal text stay literal. Malformed or unmatched markers raise explicit content errors. Prompts can nest; nested two-thirds layouts are not supported. Prompt headings and styling are implemented without adding an icon-font dependency; the decorative Rails icons are not reproduced. More complex shortcode combinations and full content/accessibility parity still require verification.

YouTube and Vimeo video entries render responsive embeds with expandable transcripts. Invalid IDs and unsupported providers produce explicit content errors rather than arbitrary embed URLs.

Video pages include a direct player link if an embedded player is blocked. Live Vimeo embed metadata and transcript toggling have been checked locally, but playback in a normal browser still needs verification; the integrated browser aborted the external iframe request.

Pre- and post-module confidence questions use a Next button without marking opinions right or wrong. Saved selections are restored when revisiting a question and can be changed. Single-select questions reject multiple submitted options server-side.

Confidence questions display the Contentful `hint` as encoded plain text, and post-module confidence questions also display `description` through the sanitized Markdown/rich-text renderer, matching Rails. Radio options reference the displayed guidance for screen readers. Missing or whitespace-only guidance is omitted; factual questions do not display these confidence-only fields.

The introduction immediately before pre-module confidence questions offers "Skip questions" to the next published `sub_module_intro`, matching the Rails optional entry route. It does not submit answers or mark the questions complete. The learning introduction's Previous link returns across the pre-confidence questions to their introduction; Previous within the questions still steps through them normally. No skip link appears on questions, interruption pages, post-module confidence checks, or when no learning introduction exists. Dedicated confidence-check start/completion/skip analytics are not yet implemented.

The module overview's Continue button resumes from the last visited page, matching the Rails milestone behaviour. New modules, or modules whose saved page was removed from Contentful, start at their first available page. Completed modules instead offer "View certificate", even after revisiting earlier content. A failed assessment offers "Retake test" via the published assessment introduction (or first summative question if no introduction exists), consistently on the overview and results page. An active retry resumes normally; passing a test without recorded module completion does not unlock the certificate button.

Formative answers become read-only after submission. Summative questions save and advance without revealing individual correctness during the test; results appear when all questions are answered. Passed assessments cannot be retaken or changed. Failed assessments can be retaken.

Learning checks redirect successful submissions to the saved question's GET page, so refreshing does not resubmit the answer. This page shows the original selection, locked options, and the question's Contentful `success_message` or `failure_message`, both immediately after submission and on later visits. Incorrect answers also show all correct option labels and, when guidance exists, an explanation through the sanitized Markdown/rich-text renderer. Invalid submissions remain on the form with validation errors and do not save an answer. Unanswered questions, confidence checks and unfinished summative questions do not show this feedback; the saved formative answer remains locked.

Summative submissions redirect to a GET results page so refreshing results does not resubmit answers. Contentful assessment-results pages use that same summary, and unfinished attempts do not display a final score.

Completed assessment results show only incorrectly answered questions, the learner's selected answer labels, and the question's Contentful `failure_message` guidance. Guidance uses the same sanitized Markdown/rich-text renderer and can link back to relevant topics. This review appears for failed attempts and passes with mistakes, but not perfect scores or unfinished attempts. It is read-only and does not expose unselected options or alter assessment answers. If an option has since been removed from Contentful, the review explicitly says it is no longer available. Razor rendering tests cover the actual results HTML.

Direct navigation to the thank-you page does not mark a module complete without a passing assessment. Certificate pages and PDF downloads require recorded module completion. Certificates include the current account name, module title, Contentful learning criteria, completion date in UK local time, and the Department for Education logo. Downloads are generated locally with Playwright/Chromium, with JavaScript and external network requests disabled, and are served with no-store caching. Full completion-flow parity remains unfinished.

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
