# AI-assisted development log

## Purpose and provenance

This log records AI-assisted work on the .NET core-journey rewrite, including
human direction, implementation decisions, validation and known limitations.

Created after development had begun. Entries up to and including
the acceptance inventory are **retrospective**, reconstructed from Git commits
and retained conversation/checkpoint summaries. They are not a verbatim prompt
transcript, a complete record of tool calls, or independently audited evidence.
Reported historical test results are checkpoint results, not fresh runs.

The assistant used was an AI assistant using Copilot SDK in VS Code. This log
does not assert a particular underlying model version.

## Working agreement

- The user set scope and priorities, requested continuation, and requested local
  checkpoint commits. AI assistance performed exploration, implementation,
  documentation and validation; this is not a claim of independent human review.
- Recreate the working Rails behaviour on `main` in a new branch, without using
  the older .NET experiments as the design authority.
- Deliver core accounts, registration, training, assessments, progress, notes and
  certificates first. Defer production data import and deployment cutover.
- Add and run failing regression tests before implementing behavioural fixes.
- Use bounded slices, regular check-ins and explicit pause notifications.
- Commit verified slices when the user requests a checkpoint. Do not push without
  instruction. Checkpoint commits include a Copilot co-author trailer.
- Update this log in every checkpoint commit, including validation, remaining
  work and any local test-data changes. Use sequential checkpoint numbers;
  retain the provenance note for backfilled history.
- Do not record credentials, tokens, private keys or learner personal data here.

## Development diary

### Checkpoint 01: foundation and local setup

The user first asked whether existing branches contained a .NET rewrite, then
requested a fresh implementation and planning exercise. Core journeys were chosen
as the first delivery; production database migration was explicitly deferred.

AI-assisted work established `rewrite/dotnet-core-journeys`, a .NET 10 MVC/Razor
solution with Domain, Application, Infrastructure and Web projects, EF
Core/PostgreSQL persistence, Contentful delivery, One Login integration code,
development sign-in, registration, assessments and a per-module learning editor.
Local PostgreSQL, HTTPS and a VS Code development task were configured.

**Decision:** server-rendered forms suit the service; Rails is the behavioural
reference, not the architecture to copy.

**Limitations:** real One Login integration was not verified. The initial notes,
account and navigation implementations were incomplete parity implementations.
The commit `3252b8c7` has the generic message `wip`; this log does
not attribute every foundation change to that commit.

### Checkpoint 02: training journeys and real content

**Checkpoint:** `bf9f5075` - Improve training journeys and real Contentful rendering.

Work included sanitized Markdown, confidence answer restoration/editing,
registration/draft guards, formative locking, summative save/advance, result
redirects, failed retries, pass locking, guarded completion and video
embeds/transcripts. Published Contentful modules were exercised locally, including
failed and passed assessment attempts.

**Limitations:** passing a local journey did not establish full Rails parity or
production readiness. Integrated-browser video playback remained unverified.

### Checkpoint 03: PDFs and richer content

**Checkpoint:** `107bc8da` - Add certificate PDFs and Contentful rich content rendering.

Work included authenticated completion-gated certificate preview/download,
Chromium PDF generation, learner/module details, recorded UK completion date,
criteria and an embedded DfE logo. PDF tests inspected actual output.

Contentful rich text and linked image assets were converted through the sanitized
Markdown pipeline. Responsive two-thirds layouts and Rails-style prompts, quotes,
buttons and external links were implemented using parsed Markdown transformations.

**Validation at checkpoint:** 108 Release tests passed; live PDF and desktop/mobile
rendering checks were performed.

**Decision:** keep raw CMS HTML disabled and report unsupported/broken content
explicitly. Decorative Rails icon fonts were not reproduced.

### Checkpoint 04: expandable guidance and assessment review

**Checkpoint:** `88316912` - Add inset guidance, expandable details and assessment feedback.

Inset text and native details followed the pinned Rails Markdown dependency's
behaviour. Keyboard toggling without JavaScript was tested at a mobile viewport.
Assessment results gained incorrect-question review, selected labels and sanitized
failure guidance, including passes with mistakes.

**Validation at checkpoint:** 130 Release tests passed after these slices; live app and
certificate checks remained healthy.

### Checkpoint 05: state-aware module navigation

**Checkpoint:** `d49ed308` - Align completed and failed module navigation with Rails.

Completed modules returned to certificates after earlier-page revisits. Failed
assessments offered the published test introduction, with first-question fallback;
active retries retained normal resume behaviour.

**Validation at checkpoint:** 138 Release tests passed, with actual Razor link tests
and a live completed-module-to-PDF check.

### Checkpoint 06: formative explanations

**Checkpoint:** `89006523` - Add Contentful explanations to learning-check feedback.

Success/failure guidance and correct labels for incorrect answers were added to
saved formative questions, with multi-select, encoding and sanitization coverage.

**Validation at checkpoint:** 149 Release tests passed. Live correct and incorrect
feedback were checked. An incorrect formative response was deliberately persisted
on the local test account; the user was informed.

### Checkpoint 07: refresh safety and confidence journeys

**Checkpoint:** `1dbb8b61` - Make learning checks refresh-safe and improve confidence journeys.

Successful formative submissions changed to POST/redirect/GET; the redundant
POST-only view was removed. Optional pre-confidence skip/back navigation was
added without submitting answers or unlocking certificates. Confidence hints and
post-confidence descriptions were mapped and rendered, with accessible guidance
references and safe handling of plain text versus Markdown/rich text.

**Validation at checkpoint:** 173 Release tests passed. Live checks verified a 302
redirect and GET-only refreshes, keyboard/mobile skip navigation, published hints,
preserved selections and validation. A correct local formative answer was saved
during refresh-safety testing; no new answers were saved during the guidance checks.

**Limitations:** dedicated confidence lifecycle records remained missing.

### Checkpoint 08: lifecycle correction, acceptance inventory and AI log

**Checkpoint commit:** `26238401` - Separate module start from page views.

#### Plan review and acceptance inventory

The user requested a review of the original plan, current progress and remaining
work. AI assistance reread the plan and inspected relevant Rails and .NET sources,
then created the [acceptance inventory](ACCEPTANCE.md).

The inventory identifies 35 cases, distinguishing implemented behaviour, partial
parity, missing features and absent integration/acceptance evidence. Significant
remaining areas include module lifecycle timing, section/topic overview rules,
account edit journeys, page-specific notes and aggregate learning log, real One
Login, PostgreSQL integration tests, complete browser journeys and accessibility.

**Validation:** 49 inventory links and unique case IDs were checked. No runtime
code changed and tests were not rerun for documentation-only work.

**Next implementation:** module-start timing, with failing tests first. Preserve
resume information and existing records. Do not change completion eligibility in
that slice.

#### Module-start lifecycle correction

**User objective:** continue with the first acceptance-inventory implementation
slice, then checkpoint it together with the acceptance inventory and AI log.

Removed module-start writes from overview visits and from generic domain page
views. The application service now explicitly starts learning only on a
`sub_module_intro` visit, using the same timestamp as its page view. The unused
standalone application start method was removed. Resume/page counts remain
independent, existing start dates survive revisits, and completion eligibility
and completion's existing domain behaviour are unchanged. No schema migration or
historical timestamp correction was made.

**Red before green:** nine of the initial thirteen selected regression cases
failed on the old behaviour. After implementation, 94 focused lifecycle/training/
Razor cases passed. A further controller transition case was added, and the full
Release suite passed **185 tests**. Editor diagnostics and whitespace checks passed.

**Manual evidence:** on the published child-development module, overview and
pre-confidence introduction visits left the local learner's module Available.
Visiting the learning introduction changed it to In progress; returning to the
confidence introduction preserved that state and its resume target. The app and
database health endpoint remained healthy. Integrated-browser click attempts
timed out, so the inspected learning destination was visited by URL; this is not
claimed as a successful click-interaction test.

**Local data change:** this verification started that module on the development
test account and recorded page visits. No answers were submitted.

Updated T04 in the [acceptance inventory](ACCEPTANCE.md) and documented the new
behaviour in the README. Automated PostgreSQL lifecycle tests, completion timing,
confidence lifecycle records and overview section availability remain outstanding.
The checkpoint includes these runtime changes, the acceptance inventory and this
log. No push was requested.

#### Checkpoint logging agreement

The user requested an AI-log update at every checkpoint and a development-diary
style for earlier entries. Entries use sequential checkpoint numbers rather than
calendar dates, with validation labels. Their backfilled provenance remains explicit;
no commit dates or evidence were changed to imply contemporaneous authorship.

**Next step:** module overview sections and availability rules, using Rails
behaviour and failing regression tests first.

### Checkpoint 09: module overview sections, availability and learning outcomes

**User objective:** continue with overview grouping, availability and metadata,
then checkpoint the verified changes.

**Checkpoint:** Group module overviews and show learning outcomes. This entry is
included in that commit; its Git history provides the resulting hash.

Replaced the flat content-page list with section headings, topic outlines,
page counts and text progress indicators. Grouping follows Rails section/topic
boundaries; interruption pages and pre-confidence/feedback sections are excluded.
Individual content pages/questions are no longer overview links.

Saved visited-page names now flow from the progress store through the application
listing and controller into the overview. Incomplete topics remain plain text;
completed topics link to their headers. Persisted module completion makes topics
revisitable even without individual historical visit records. Failed assessment
status and passed reflection availability are represented. Direct-URL access,
resume CTAs, assessment submissions and completion eligibility are unchanged.

**Red before green:** six actual-Razor cases failed on the flat overview, and a
separate controller case failed because visited names were absent. Implemented the
grouping and plumbing, then added boundary/order/count, removed-content and direct
access regressions. A Razor variable-name compilation error was corrected during
implementation. The full Release suite passed **196 tests**.

**Manual evidence:** all eight published module overviews returned successfully
with grouped sections and topic lists. The completed brain-development module
exposed all ten topics; incomplete modules did not expose unfinished topic links.
The child-development overview had no horizontal overflow at 375px. App/database
health remained healthy. These checks viewed overviews only; no answers or new
learning starts were recorded.

**Remaining:** module descriptions/outcomes, grouped module listing, broader
accessibility acceptance and automated PostgreSQL overview evidence. Updated the
acceptance inventory and README. Changes remain uncommitted, including the earlier
checkpoint-number formatting update to this log.

#### Module overview descriptions and learning outcomes

Continued the same uncommitted overview checkpoint with Contentful module
description/outcomes mapping and overview display of description, outcomes and
coverage criteria. All fields use the existing sanitized Markdown/rich-text
pipeline. Optional empty metadata does not create empty headings or change module
availability. Existing navigation, assessment and certificate rules are unchanged.

**Red before green:** four of seven new metadata cases failed before implementation.
After mapping/rendering changes, 79 focused tests passed and the full Release suite
passed **203 tests**, including PDF regressions. Editor diagnostics and whitespace
checks passed.

**Manual evidence:** all eight published module overviews displayed description,
outcomes and criteria successfully. The completed brain-development overview
retained its topics and certificate CTA, with no horizontal overflow at 375px.
App/database health remained healthy. No answers or new module starts were recorded.

Updated T02 to Implemented for the enumerated functional cases; visual,
accessibility and database acceptance remain separate. Grouped course-list
presentation is still outstanding. The user requested this checkpoint after
verification; no push was requested.

**Checkpoint validation:** latest full Release result is 203 passed; whitespace
checks passed. Includes the preceding overview slice and checkpoint-number log
formatting. Next: group My modules by learning state.

### Checkpoint 10: My modules learning-state groups

**User objective:** continue with grouped course-list presentation.

Replaced the flat module list with in-progress, available and completed sections,
ordered by content position within each group. Added new/returning learner empty
guidance and an all-completed message, while preserving the explicit no-content
message. Section headings are labelled regions and module headings are level
three. Overview links, recorded page counts and persisted lifecycle state are
unchanged.

**Red before green:** four of the first five actual-Razor cases failed on the
flat list. Added two further cases for returning learners and encoded titles/
overview links. A new test initially expected the wrong fixture slug; corrected
that expectation. The full Release suite passed **210 tests**. Editor diagnostics
and whitespace checks passed.

**Manual evidence:** the authenticated local learner's list showed one in-progress,
six available and one completed module, with eight overview links and no horizontal
overflow at 375px. App/database health remained healthy after restart. Viewed the
list only; no answers or new learning starts were recorded.

Updated T01 and README without claiming full listing parity. Future releases,
completed-module dates/certificate table, assessment-specific list actions and
broader accessibility/database acceptance remain outstanding. This slice is
uncommitted; no push was requested.

#### Completed-module dates and certificate actions

Continued the same uncommitted checkpoint with the Rails completed-module table
behaviour: module overview links, saved completion dates and View certificate
actions. The application listing and controller now carry the existing completion
timestamp; no schema or completion-timing changes were made. The table uses
labelled column/row headers and responsive wrapping. A missing timestamp on a
completed view model raises an explicit error rather than displaying an invented
date.

Extracted the existing certificate UK local-date conversion into a shared helper
so certificates and the list remain consistent across British Summer Time.

**Red before green:** all four initial table/date-plumbing checks failed before
implementation. A subsequent summer-midnight case also failed before sharing the
date conversion. Corrected a misplaced Razor branch caught by compilation during
implementation. The final full Release suite passed **215 tests**, including
existing PDF and winter/summer certificate tests. Editor diagnostics and whitespace
checks passed.

**Manual evidence:** the live completed brain-development module showed its saved
date and certificate action. No horizontal overflow was measured at 320px, 375px
or 1280px. The certificate destination displayed the matching completion date.
The integrated-browser click and screenshot scroll timed out while the shared
page was not visible; the destination was checked by direct URL instead, not
claimed as a successful click test or visual screenshot review. App/database
health remained healthy. No answers, progress changes or new starts were made.

Updated T01 and README. Future-release metadata, assessment-specific list actions
and full accessibility/PostgreSQL acceptance remain outstanding. Changes are still
uncommitted at the end of implementation and nothing was pushed.

**Checkpoint:** the user requested a checkpoint of these verified changes.
Commit message: Group learner modules and show completed module certificates.
This log is included in that commit; Git history supplies its resulting hash.
Latest full Release result: **215 passed**; whitespace checks passed. No push
requested. Next: investigate authoritative Rails release rules and remaining
assessment-specific list actions.

### Checkpoint 11: assessment actions and future-module cards

**User objective:** checkpoint the verified module list and continue.

Saved Checkpoint 10 as `3f0c1cc8` (Group learner modules and show completed module
certificates), including this log. Verified a clean worktree immediately after
commit. Nothing pushed.

Compared Rails course progress and the list-card helper. Future modules are
classified through draft/content validity, not a simple release-date gate;
the release record's first-published timestamp is separate. Deferred translating
those content-integrity rules to a dedicated slice.

Added latest-assessment plumbing for in-progress list items, using the existing
authenticated account and assessment service. Graded failures show Retake end of
module test, linking to the introduction or first summative question. Graded passes
show View previous test result. Available/completed modules and ungraded attempts
do not expose these actions. Existing overview links, certificate table, grading,
retry lifecycle and completion rules are unchanged. Assessment reads are awaited
sequentially for active modules because the scoped EF context cannot run concurrent
queries.

**Red before green:** four of five initial controller/Razor cases failed before
implementation. Corrected the new rendering test's introduction slug to match its
fixture. Added state-suppression and missing-introduction fallback coverage.
Full Release suite: **223 passed**. Editor diagnostics and whitespace checks
passed.

**Manual evidence:** restarted app/database health was healthy. The local list
retained one in-progress, six available and one completed module with no horizontal
overflow at 375px. That learner has no graded test on its active module, so no
assessment action appeared; pass/fail actions were verified by automated actual-
Razor and controller tests, not by changing local answers. No new learning starts
or answers were saved.

Updated T01 and README. This continuation remains uncommitted. Next: translate
Rails content-integrity/future-module rules and upcoming-text presentation.

#### Future-module presentation using the existing draft rule

Investigated Rails `ContentIntegrity`, course progress, upcoming cards and route
guards. Its full release validator requires artwork/marketing metadata, specific
page positions, ten summative questions, feedback and confidence counts. Switching
all those rules on would change the existing optional-metadata contract and could
hide otherwise working modules. A clarification request could not be answered
because the user was unavailable; chose the lower-risk phased implementation
rather than claiming approval for stricter release gates.

Mapped optional Contentful `upcoming` text. Named modules already classified as
draft now appear in an ordered Future modules in this course group with Coming
soon headings and encoded plain text. No training links, progress counts or
assessment/certificate actions appear there. Draft classification takes precedence
over historical progress only for listing state; saved timestamps are unchanged.
Existing draft route guards still block overview, content, answers, assessment
results and certificates. Future modules prevent the all-completed message, and
the empty available-group message appears when appropriate.

**Red before green:** five of six initial mapping/service/controller/actual-Razor
cases failed before implementation. Added unresolved references, mixed available/
upcoming groups, started/completed historical progress and result-route guard
coverage. Full Release suite: **232 passed**. Editor diagnostics and whitespace
checks passed.

**Manual evidence:** restarted app/database health was healthy. The current feed
still showed one in-progress, six available and one completed module with no
horizontal overflow at 375px. All eight module overview URLs returned HTTP 200.
No draft modules exist in that feed, so future cards
are verified by automated actual-Razor/mapping/controller tests, not claimed as
live CMS evidence. No answers or new learning starts were saved.

Updated README and T01, which remains Partial. Full Rails integrity classification
is explicitly deferred; existing optional metadata and working-module availability
are preserved. This continuation remains uncommitted; nothing pushed.

**Checkpoint:** the user requested a checkpoint of both continuation slices.
Commit message: Add module assessment actions and future-module cards.
This log is included in that commit; Git history supplies its resulting hash.
Latest full Release result: **232 passed**; whitespace checks passed. No push
requested. Full Rails release-integrity classification remains outstanding.

### Checkpoint 12: account name, preferences and location editing

**User objective:** continue after the module-list checkpoint. A choice between
account editing and stricter release checks could not be answered because the user
was unavailable; chose the bounded name-edit gap without tightening module access.

The existing Change link sent registered learners through registration and onward
to location. Added authenticated GET/POST `/account/name` using the existing name
model, account service and shared registration form. Account mode prefills saved
values, offers Save/Cancel and certificate/One Login guidance, and returns to
My account with confirmation after saving. Missing accounts return Not Found;
unregistered learners return to registration. Registration mode retains Continue
and its existing next step. No schema or identity-provider update was introduced.

**Red before green:** six of seven controller/actual-Razor checks failed before
implementation; the missing-account check already passed against the endpoint
skeleton. Corrected misplaced controller edits caught by compilation during
implementation. Full Release suite: **239 passed**. Editor diagnostics and
whitespace checks passed.

**Manual evidence:** live HTTP submissions rejected whitespace-only and overlong
names with field errors, accepted the existing unchanged name and redirected to
My account with confirmation, and rejected a missing anti-forgery token with 400.
Reopening the form showed the same saved name. Registration still posts to its
own name route with Continue. Account edit had no overflow at 375px; app/database
health remained healthy. These were HTTP fetch checks through the authenticated
browser, not a claimed successful UI click journey.

**Local data:** saved the development learner's existing name unchanged; no
training progress, answers or new starts were recorded. Updated README and A07,
which remains Partial for location, setting, role and preference edits.
Changes remain uncommitted; nothing pushed.

#### Independent email and research preference editing

Continued the uncommitted account checkpoint with separate email/research edit
routes, matching Rails' independent preference journeys. My account now displays
both saved choices and change links for registered learners. A shared single-
preference form/model prefills yes/no, requires a choice and offers Save/Cancel.
Each update preserves the other saved preference through the existing account
service. Authentication, registration, current-account and anti-forgery guards
apply. Registration's combined preference form/navigation is unchanged.

**Red before green:** all four initial actual-Razor preference cases failed before
implementation. Added controller cases for both values on both preferences,
preserving the opposite value, missing selections and registered-account guards.
Full Release suite: **251 passed**. Editor diagnostics and whitespace checks passed.

**Manual evidence:** authenticated HTTP checks on both routes showed errors for
missing/malformed selections, successful unchanged-value saves returning to
My account with confirmation, persisted unchanged selections and 400 for missing
anti-forgery tokens. Research form radio IDs are unique, Cancel targets My account,
and no overflow was measured at 375px. App/database health remained healthy.
These are HTTP/rendering checks, not a claimed UI click test.

**Local data:** saved the development learner's existing email/research choices
unchanged. No training answers, progress or new starts were recorded.
Updated README/A07; location/setting/role account edits remain outstanding.
Changes remain uncommitted; nothing pushed.

#### Account country and local-authority editing

Continued the same uncommitted checkpoint with account location display/editing.
Rails saves country then redirects through setting-edit steps. A clarification
request about a combined save could not be answered because the user was
unavailable; chose a bounded atomic country/authority edit rather than leaving
an already-registered account without a newly required English authority.
This UX difference is documented and still needs human review.

Added `/account/location` GET/POST and a dedicated form backed by the existing
registration reference provider. Saved names map to IDs for prefill. Validation
uses the persisted setting requirement, never the submitted flag; invalid choices
leave both saved values unchanged. Non-English or non-required authority data is
cleared on save. Setting/role fields, registration completion and registration
navigation are unchanged. My account shows country and the applicable authority
or Not applicable. Save returns with confirmation; Cancel does not submit.

**Red before green:** all nine initial controller/actual-Razor cases failed before
implementation. Corrected an assertion that assumed HTML attribute ordering, then
added three account-summary cases. Full Release suite: **263 passed**. Editor
diagnostics and whitespace checks passed.

**Manual evidence:** live HTTP checks rejected a missing required authority even
with a forged false requirement flag, rejected an invalid country, accepted an
unchanged-value save returning to My account with confirmation and rejected a
missing anti-forgery token with 400. Reopening retained both selections. Account
summary displayed England/York and its edit link. No overflow at 375px;
app/database health remained healthy. These are HTTP/rendering checks, not a
claimed UI click journey.

**Local data:** saved the development learner's existing country and authority
unchanged; no training answers, progress or new starts were recorded.
Updated README/A07; setting/role edits and full account acceptance remain
outstanding. Changes remain uncommitted; nothing pushed.

**Checkpoint:** the user requested a checkpoint of these three account slices.
Commit message: Add account name preference and location editing.
This log is included in that commit; Git history supplies its resulting hash.
Latest full Release result: **263 passed**; whitespace checks passed.
Nothing pushed. Setting/role editing and human review of the combined location
form remain outstanding.

## Future entry format

At subsequent checkpoints, append:

- Next sequential checkpoint number, user-requested objective and checkpoint commit, if any.
- Changes and significant decisions, including approved behaviour differences.
- Tests actually run and their outcomes; distinguish automated and manual evidence.
- Remaining gaps, blockers, local test-data changes and the next step.

Keep entries concise and evidence-based. Do not treat generated code, test totals
or this log as proof of human review, complete parity or production readiness.
