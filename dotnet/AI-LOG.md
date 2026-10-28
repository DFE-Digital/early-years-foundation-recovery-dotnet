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

### Checkpoint 13: account role, setting and registration improvements

**User objective:** checkpoint the account slices and continue.

Saved Checkpoint 12 as `703a778a` (Add account name preference and location
editing), including this log. Verified a clean worktree immediately after commit.
Nothing pushed.

Added authenticated `/account/role` GET/POST and a combined role/experience form.
Both values validate before a single account-store save; setting, country,
authority and registration completion remain unchanged. Roles come from the
persisted setting group, not posted choices. The general group receives an explicit
other option because the current reference list omits one despite domain support.
Custom descriptions are required for other, encoded on display and cleared by a
standard role. The description field is available without JavaScript. No-role
settings show Not applicable and redirect attempted edits to My account.

My account displays role/custom description, the reference-data experience label
and an applicable edit link. Save returns with confirmation; Cancel does not submit.
This intentionally combines Rails' separate role/experience steps for a bounded
account save; it is documented for human review, not claimed as approved UX parity.
Registration routes/forms are unchanged.

**Red before green:** all eight initial controller/actual-Razor cases failed before
implementation. Added childminder group/forged-choice and account-summary encoding/
applicability coverage. Full Release suite: **274 passed**. Editor diagnostics and
whitespace checks passed.

**Manual evidence:** live HTTP checks on the childminder test account rejected
an out-of-group role and an invalid experience choice, accepted an unchanged-value
save returning with confirmation, retained both selections and rejected a missing
anti-forgery token with 400. Account summary displayed Childminder and the
10 years or more label. No overflow at 375px; app/database health remained healthy.
Custom-role behaviour is automated controller/Razor evidence, not live CMS/account
evidence. These are HTTP checks, not a claimed UI click journey.

**Local data:** saved the development learner's existing role/experience unchanged;
no training answers, progress or new starts were recorded. Updated README/A07.
Setting editing and full account acceptance remain outstanding.
This continuation is uncommitted; nothing pushed.

#### Setting editing with an atomic dependent-answer save

**User objective:** continue. Compared Rails setting controller/form and setting
metadata: Rails saves the setting before collecting dependent answers, resetting
experience and clearing non-applicable fields. A clarification request about
separate steps versus an atomic save could not be answered because the user was
unavailable. Chose the conservative atomic approach consistent with the recent
account forms; this is a documented UX difference, not approved Rails parity.

My account now shows the setting title or encoded custom description. Added
authenticated GET/POST `/account/setting`: choose a setting, then complete/review
its applicable authority, role and experience before an explicit Save. Continue
and Cancel do not persist anything. Unchanged settings prefill saved dependent
answers; different settings collect fresh ones. The selected reference setting
and persisted country determine requirements, ignoring posted flags/choice lists.
Other settings require a description; existing custom role rules are shared with
the role edit. Invalid submissions leave all saved answers untouched. A single
store save applies the setting and valid dependent answers, clearing irrelevant
authority, role, experience and custom descriptions. Name, country, preferences
and registration completion are preserved. Registration remains unchanged.

**Red before green:** all 16 initial controller/actual-Razor cases failed against
endpoint skeletons/missing view. A further regression test caught a field-length
error returning to the choice screen instead of keeping dependent inputs visible;
fixed the selection/detail validation boundary. Added single-save, no-role/
authority, encoding and no-dependent-answer form coverage. Full Release suite:
**296 passed**. Editor diagnostics and whitespace checks passed.

**Manual evidence:** live authenticated HTTP checks showed that previewing another
setting leaves the account summary unchanged; invalid setting, out-of-group role
and forged authority-requirement submissions returned field errors. Saving the
original setting/answers returned to My account with confirmation; reopening
retained the original selections. Missing anti-forgery token returned 400.
The detail form had no horizontal overflow at 375px. Tests used browser fetch and
programmatic form submission, not a claimed successful click journey. App/database
health remained healthy. Custom-setting saves and conditional clearing are
automated evidence, not claimed live learner mutations.

Updated README/A07; A07 remains Partial for human UX/accessibility and full
HTTP/PostgreSQL acceptance. The development learner's existing answers were saved
unchanged; no training answers, progress or new starts were recorded. This slice
and the prior role/experience slice remain uncommitted; nothing pushed.

#### Conditional registration: custom roles and authority revisits

**User objective:** continue. Compared the Rails registration system cases and
setting/authority/role controller branches with the .NET flow. Found the general
role reference list omitted other, making the custom-role journey impossible;
authority GET used a saved display name where the form expected an ID; authority
POST lacked GET's applicability guard and could throw on skipped branches.

Extracted the existing account role-choice helper for registration/account reuse.
General registration roles now offer other with the existing required/200-character
description rules and trimmed persistence; the description is available without
JavaScript. Childminders retain only their reference roles. The combined .NET
role/custom-description screen remains an explicit difference from Rails'
separate custom-role screen. Standard roles clear old custom text.

Authority revisits map saved names to choice IDs. Authority POST now resolves the
current account and skips inapplicable country/setting branches before validating
or mutating, matching GET. Invalid applicable choices preserve the saved answer
and re-render reference choices. No schema or registration screen-sequence changes.

**Red before green:** nine of 17 initial focused cases failed before implementation
(the remaining eight established existing branch behaviour). Added completion
coverage for standard England, non-English custom role, childminder, English
authority/no-role, non-English no-role and custom setting/role paths. Added stale
role clearing, no-role POST guards and setting-change/incomplete replacement
coverage. Controller journeys use in-memory stores and do not simulate HTTP model
binding; actual-Razor cases cover both role groups. Full Release suite:
**316 passed**. Editor diagnostics and whitespace checks passed.

**Manual evidence:** restarted app/database health was healthy. The live
registration authority form preselected York. Authenticated HTTP submissions
rejected an unknown authority and a custom role for the childminder account,
and missing anti-forgery returned 400. Saved account details were unchanged.
No overflow at 375px. General/custom-role completion is automated evidence,
not a claimed live registration or successful click journey. No answers, progress,
new starts or account changes were saved.

Updated README/A04/A05. Full browser/PostgreSQL registration, displayed answer
summaries, review/resume sequencing and human UX acceptance remain outstanding.
All continuation changes remain uncommitted; nothing pushed.

#### Registration answer summary and rejected-completion rendering

**User objective:** continue. Read Rails check-your-answers/base controllers and
request cases. Rails review mode returns to the summary after collecting newly
required answers. Kept that routing change separate from this bounded presentation
slice rather than adding session state or claiming full review parity.

Check your answers now uses the setting title/custom description and friendly
experience label, with the existing saved-value fallback for legacy reference IDs.
Preferences display Yes/No, while unanswered fields explicitly say Not answered.
Authority appears only when required in England; role and experience are omitted
for no-role settings. Each visible answer has an explicit, distinctly labelled
registration Change link, including the previously missing authority and research
links. Both preference links retain the existing combined preferences form.
Custom descriptions remain encoded. Shared controller summary population runs on
GET and rejected completion so the setting/experience labels do not disappear
when validation fails. Completion rules and forward step routing are unchanged.

**Red before green:** nine of ten initial controller/actual-Razor cases failed
before implementation; the missing-account guard already passed. Added actual
error-summary rendering with a missing authority and retained display labels.
Coverage includes conditional rows/edit links, encoding, null and false preference
distinction, missing answers and legacy-ID fallback. Full Release suite:
**327 passed**. Editor diagnostics and whitespace checks passed.

**Manual evidence:** restarted app/database health was healthy. Live summary showed
the current setting title, York, Childminder, 10 years or more and Yes preferences.
All nine displayed edit destinations returned HTTP 200; their labels were unique.
The form retained its registration completion destination and anti-forgery input.
No horizontal overflow at 375px. These were read-only browser/HTTP checks, not a
claimed click/save journey or a new registration. No account data, training
answers, progress or learning starts were changed. Conditional/missing-answer
summaries and rejected completion are automated evidence, not live data mutations.

Updated README/A04/A05. Return-to-summary review/resume sequencing, full
browser/PostgreSQL registration and human UX/accessibility acceptance remain
outstanding. All continuation changes remain uncommitted; nothing pushed.

**Checkpoint:** the user requested a checkpoint of the role/experience, setting,
conditional registration and answer-summary slices.
Commit message: Add account setting edits and improve registration journeys.
This log is included in that commit; Git history supplies its resulting hash.
Latest full Release result: **327 passed**; checkpoint whitespace checks passed.
Nothing pushed. Next: return-to-summary review/resume sequencing, including newly
required answers after country or setting changes. Human UX/accessibility review
and full browser/PostgreSQL acceptance remain outstanding.

### Checkpoint 14: registration review and deferred-feedback boundary

**User objective:** continue after Checkpoint 13 (`883bb795`). Verified the rewrite
branch and clean worktree before editing. Compared Rails review/resume rules.

Summary Change links now start review mode for unfinished registrations. Successful
edits collect the first outstanding required answer, otherwise return directly to
the summary. Country/setting changes collect newly required authority, role and
experience; non-applicable fields retain the existing clearing behaviour. Terms,
custom descriptions and both explicit preferences participate in routing.
Validation errors retain review mode. Viewing the summary, rejected-completion
summary rendering or successful completion clears it. Review does not itself
complete registration; normal forward registration and completed accounts retain
their previous routing.

Used the existing protected cookie TempData provider for a subject-bound marker
rather than adding server sessions. The fixed review query value is not a return
URL; redirects use named local actions only. A different authenticated subject
cannot use the marker. This differs internally from Rails' session storage but
preserves the reviewed journey behaviour and existing .NET combined forms.

**Red before green:** five of seven initial controller review cases failed;
completed-account and other-subject cases already passed. Added missing terms/
preferences and marker-clearing coverage. Four isolated HTTP tests use in-memory
accounts, sample reference data and test authentication with real cookie TempData,
MVC binding and anti-forgery. They verify invalid-then-valid name edit, review end/
normal-flow restoration, setting-to-role/experience progression, country-to-
authority progression and missing-token rejection. Actual-Razor tests verify
review URLs on applicable summary links. Full Release suite: **339 passed**.
Editor diagnostics and whitespace checks passed.

**Manual evidence:** restarted local app/database health was healthy. The shared
browser session had expired; signed back in with the development test-user form.
All nine live summary edit links carried the review marker, with no overflow at
375px. No learner answers or training state were changed. The live learner is
already registered, so unfinished-review save behaviour is isolated automated HTTP
evidence, not a claimed live registration/browser click journey.

Updated README/A04/A06; A06 is now Partial, not complete. PostgreSQL restart/resume,
full direct-step sequencing and browser/UX acceptance remain outstanding.
This continuation is uncommitted; nothing pushed.

#### Explicit deferred-feedback boundary

**User objective:** continue after reviewing the remaining plan. Asked whether to
move completion from thank-you to certificate access while retaining the pass guard.
The user was unavailable; did not treat that as approval to change completion
semantics. Kept timing, eligibility and existing recorded completion dates unchanged.

Implemented the safer deferred-feedback boundary: published feedback pages now use
a neutral Module feedback heading and explain that collection is unavailable and
no feedback has been submitted. CMS question bodies/options and forms are not
displayed. Continue without submitting feedback links to the first following
non-feedback page, skipping only consecutive feedback pages; with no continuation,
Return to module is shown. Normal confidence, thank-you and certificate routes
retain their behaviour/guards. No answers, assessments or completion are written
by viewing this boundary; the existing page visit/resume write remains, and skipped
feedback pages are not marked visited. This is a documented interim boundary
requiring human UX review, not implementation of the deferred feedback workflow.

**Red before green:** both initial actual-Razor cases failed on the missing notice;
the existing controller rejection/eligibility case passed. Added continuation
boundary tests for confidence, thank-you, certificate, ordinary and unlisted pages,
no-continuation fallback, neutral heading and suppression of CMS content/forms.
Controller coverage verifies feedback POST rejection, only the viewed page being
recorded, no assessment/completion, and unchanged pass/certificate guards.
Full Release suite: **347 passed**. Editor diagnostics and whitespace checks passed.

Restarted the local app and checked database/app health. Did not visit feedback
pages on the shared learner because that would change saved page/resume state;
feedback presentation/navigation evidence is actual-Razor/model/controller tests,
not a claimed live CMS/browser journey. No learner answers or training state were
changed. Refreshed the acceptance inventory's stale baseline/test totals and
documented T10 as Partial pending human/end-to-end acceptance.

README and acceptance inventory updated. Completion timing still requires a user
decision; page-specific notes/consolidated log remain the next substantial feature.
This slice and registration review sequencing remain uncommitted; nothing pushed.

**Checkpoint:** the user requested a checkpoint of both verified slices.
Commit message: Add registration review sequencing and deferred feedback boundary.
This log is included in that commit; Git history supplies its resulting hash.
Latest full Release result: **347 passed**; checkpoint whitespace checks passed.
Nothing pushed. Next substantial feature: page-specific notes and consolidated
learning log. Completion timing still requires an explicit decision; full
PostgreSQL/browser and human UX/accessibility acceptance remain outstanding.

### Checkpoint 15 (completed with Checkpoint 16): learning notes and consolidated log

**User objective:** continue after Checkpoint 14 (`3e5d5240`). Compared Rails note
identity, the flagged-page rule, forms, save/navigation behaviour and active
module learning log with .NET.

Contentful's localized notes flag now enables the editor only on text and topic
introduction pages. Notes are identified by learner/module/page, with the CMS
title retained. Save and continue updates an existing note instead of duplicating
it, and follows the server-derived next page or module overview. Empty text clears
the current page's note; validation retains submitted text without overwriting
saved content. Posted title, learner and navigation values are not trusted.
Saving a note does not itself write progress, assessments or completion.

The additive EF migration gives existing module notes an empty page name, keeping
their content and separate module editor. That editor now checks registration
completion on both GET and POST, matching the training guard. No learner notes,
answers or progress were changed in the shared local account.

**Evidence:** the initial actual-Razor enabled-editor regression failed before
the editor was wired. Focused tests cover notes flags, identity/separation,
repeat edits, legacy preservation, clearing, the exact 20,000-character boundary,
registration/draft/page guards and CMS navigation. A real HTTP test with isolated
in-memory stores covers form binding, anti-forgery, encoded redisplay, validation,
reopening, clearing and ignored spoofed fields. Full Release suite: **373 passed**.
Editor diagnostics and whitespace checks passed.

Restored the repository-declared EF tool after the initial command reported it
missing. Generated/applied the migration to local PostgreSQL; EF reports no
pending model changes. Restarted the development app; app/database health is
healthy. Did not visit shared training pages merely for screenshots, because GET
updates resume state. Local schema/health checks are not claimed as database
ownership, restart durability or a full live browser/CMS journey.

Added `/my-account/learning-log`, grouping notes under all published modules a
registered learner has started, including modules with no notes. The aggregate
includes dates and links to current Contentful pages; removed/renamed page notes
remain visible without a stale link. Preserved module-level notes remain editable
from their group. A header navigation link is hidden until the learner has
completed registration and started published training. The module overview now
links to the aggregate. Groups follow module position and notes follow most
recent update order. The layout uses semantic sections rather than duplicating
Rails' tab widget; no JavaScript tab behaviour was added.

**Additional evidence:** service tests cover started/published module selection,
module ordering, empty groups, draft exclusion and cross-user isolation.
Rendered-Razor tests cover empty states, encoding, dates, page links and preserved
legacy-note editing. Isolated HTTP coverage exercises the real shared layout:
navigation hidden before progress, visible after progress, and aggregate state.
The same HTTP case posts an edit through the retained legacy editor and confirms
the note changes. Full Release suite: **378 passed**. EF reports no pending model
changes; editor diagnostics and whitespace checks passed.

README and N01-N03 updated. PostgreSQL concurrency, durability, ownership/closure,
save failures and human UX/accessibility acceptance remain outstanding. This
slice is included in the Checkpoint 16 commit below; nothing was pushed.

### Checkpoint 16: confidence-check lifecycle state

**User objective:** continue after the notes/log slice, then checkpoint. Compared Rails'
`ConfidenceCheckProgress` transitions and page-view triggers with the current
editable .NET confidence answers.

Added one `ConfidenceCheckProgress` row per learner/module/check type (`pre` or
`post`), with first-start, completion and optional pre-check skip timestamps.
First-question GET records start; last-question GET records completion. For a
single-question check, the first GET starts and a later GET completes, matching
the Rails `if/elsif` event ordering. A completion reached without a prior start
also records a start timestamp. The first `sub_module_intro` marks pre-check
skipped only if pre-confidence questions exist and no start/completion was
recorded; re-entering the intro is idempotent. Skipped checks may later be
started. Answer selections remain separate, editable response records; no
analytics or reporting events are claimed.

Added an FK-cascaded table and unique user/module/check-type index. Generated and
applied the migration to local PostgreSQL; EF reports no pending model changes.
The schema change affects no existing learner records. No shared learner answer,
progress or confidence data was changed for this check.

**Evidence:** service tests cover distinct pre/post rows, multi-question and
single-question starts/completions, repeated page views, first-start skip,
revisits, later start after skip and no skip when pre-check was started or absent.
Controller tests exercise the real page-view entry point. EF model metadata
asserts the unique identity. Full Release suite: **382 passed**; editor
diagnostics and whitespace checks passed. Local app/database health is healthy.
This is not yet a PostgreSQL-backed journey, restart or concurrent-update test;
those remain in acceptance.

README and T07-T09 updated.

**Checkpoint:** the user requested a checkpoint of the notes, consolidated log
and confidence lifecycle changes together.
Commit message: Add learning log and confidence check lifecycle.
This log is included in that commit; Git history supplies its resulting hash.
Latest full Release result: **382 passed**; migration/model and whitespace checks
passed. The local app/database is healthy. Nothing pushed.

### Checkpoint 17 (in progress): isolated PostgreSQL persistence evidence

**User objective:** continue with acceptance testing rather than expanding core
features. Added four integration cases using the real EF/Npgsql stores and
application services. Each case creates a uniquely named database, applies
migrations and drops that exact database on disposal. The administrative
connection comes from `EYFR_TEST_POSTGRES`; absent configuration explicitly skips
the cases. Configured connection failures fail rather than silently skip.

Cases verify page and legacy notes across fresh contexts, repeat edits without
duplicate rows, clearing, per-page/per-module/per-learner isolation, editable
confidence answers and idempotent lifecycle timestamps, later start after skip,
and deletion of only the closing learner's notes through AccountService. A
separate initial-schema upgrade test inserts an old module note before applying
the later migrations, then proves it survives alongside a new page note.

The first targeted run passed three cases; the closure test correctly rejected
the incomplete fixture because no closure reason had been chosen. Updated the
fixture to use the real reason-selection step before closing; no production
behaviour was changed. Full Release run with PostgreSQL enabled: **386 passed,
none skipped**. Editor diagnostics and whitespace checks passed. A separate run
without configuration explicitly skipped all four cases. Verified no
`eyfr_integration_...` databases remained and the development health check was
healthy.

CI now declares PostgreSQL 17 and sets the integration connection. Remote CI
success is not claimed. README documents permissions, isolation, invocation and
explicit skip behaviour. These are fresh-context persistence tests, not app or
database restart tests, simultaneous-write tests or a complete browser journey.
No shared development learner data was used or changed.

Updated N03/Q02 to Partial with the remaining restart, concurrency and failure
acceptance explicit. Changes remain uncommitted; nothing pushed.

### Checkpoint 18 (in progress): persisted assessments, retries and completion

**User objective:** continue acceptance evidence for the core learner journeys.
Extended the isolated PostgreSQL fixture with deterministic formative and
summative questions, including a multi-select question. Added three cases using
the real application service and EF stores, reopening contexts between steps:

- An interrupted summative attempt retains its identity/start, saved answer and
  resume position. Replacing the saved answer with multiple selections leaves
  one answer for that question and one visited-page record. The final question
  grades the same attempt, without automatically completing the module.
- A 50% failed attempt remains unchanged while a distinct retry is started,
  reopened and passed at 100%. Incomplete/failed attempts cannot complete the
  module. Completion after passing survives a fresh context and repeat calls
  preserve both completion and assessment timestamps. Later wrong submissions
  cannot mutate the pass or create another attempt. Another learner's partial
  attempt remains separate and unchanged.
- Formative questions retain the first saved answer across contexts, even after
  a different answer is submitted; another learner can independently answer
  correctly. Exactly one response per learner is persisted in this scenario.

The first build caught an xUnit analyzer rule for filtering before Assert.Single;
used its predicate overload. All seven PostgreSQL tests then passed, followed by
the complete Release suite: **389 passed, none skipped**. No production code,
schema or completion timing was changed. Updated README and S02-S05/Q02 evidence.
These tests establish fresh-context persistence, not process/database restarts,
simultaneous writes or end-to-end HTTP/browser/certificate acceptance.
The existing development learner was not used. Changes, including Checkpoint 17,
remain uncommitted; nothing pushed.

### Checkpoint 19 (in progress): PostgreSQL-backed core HTTP journey

**User objective:** continue with deterministic registration-to-certificate
acceptance. Added a partial test-class file sharing the existing unique-database
lifecycle and deterministic module fixture, extended with results/thank-you/
certificate pages. The fixture runs the real MVC application with fixed training
and registration content; only those providers and the connection are replaced.
No fake authentication, repositories or PDF renderer are used.

The HTTP case exercises anonymous and registration guards; actual Development
sign-in/cookie authentication; every England/independent-childminder registration
step including authority, role, experience, explicit preferences and summary;
page-note save/log; rejection of a missing anti-forgery token; an interrupted
assessment with sign-out/sign-in and restored selection; failure/retry/pass and
result refresh; certificate denial before completion; thank-you completion; and
an actual Chromium PDF download with learner/module text, A4 dimensions,
attachment and no-store checks. Fresh contexts verify persisted registration,
attempt history, scores, completion and notes.

Extended the same case through name editing (including updated certificate
preview), closure reason/confirmation, returning to the cancellation destination
without closing, final redaction/note deletion and sign-out. Fresh database reads
verify closure and personal-data redaction. This does not claim that every
personal-data field is redacted, or prove provider-side logout.

Targeted HTTP case passed before and after the edit/closure extension.
Full Release suite with PostgreSQL enabled: **390 passed, none skipped**.
No production code, migrations or lifecycle decisions changed. Q03 moves from
Missing to Partial and A08 from Unverified to Partial; README and acceptance
evidence explain that this is in-process HTTP using Development sign-in, not
browser interaction, live One Login, all registration branches, process restart
or concurrency evidence. The shared local learner was not used.
Changes, including Checkpoints 17-18, remain uncommitted; nothing pushed.

### Checkpoint 20 (in progress): confidence and learning-check HTTP acceptance

**User objective:** continue core learner behaviour acceptance, keeping
infrastructure work distinct from learner journeys. Extended the existing
registration-to-certificate HTTP case rather than adding backend features.

The deterministic fixture now supplies valid choices for every confidence
question and explicit formative guidance. The journey submits both pre-check
questions, revisits/edits a saved selection, verifies editable inputs without
correctness feedback, and checks confidence alone has neither started learning
nor created an assessment/completion. It then starts learning, submits/revisits/
edits the post-check and verifies latest saved selections plus independent
pre/post completion records. Completing confidence checks cannot unlock the PDF.

The formative portion submits no answer and checks visible validation, then
submits an incorrect answer and follows POST/redirect/GET to restored disabled
controls, correct-answer/explanation headings and guidance. A later submission
of a different answer still shows the original choice/explanation; a fresh
context verifies exactly one formative response with the original answer.
Neither confidence nor formative activity creates a summative attempt or
completes the module. The existing failure/retry/certificate/edit/closure
journey then continues.

Targeted HTTP case passed, then the full Release suite passed **390 tests,
none skipped**, with PostgreSQL enabled. The count is unchanged because this
extends the existing end-to-end case. Editor diagnostics passed. No production
behaviour/schema changes or shared development learner updates. Updated T07,
S02/Q03 and README; browser interaction, optional skip navigation in the combined
journey, remaining registration branches and real One Login are still explicit
gaps. Changes remain uncommitted; nothing pushed.

#### Checkpoint commit for slices 17-20

User requested "checkpoint continue". Re-ran the complete Release suite with
PostgreSQL enabled: **390 passed, none skipped**; whitespace checks passed.
Checkpoint commit: `Add PostgreSQL and full learner HTTP acceptance tests`.
This commits the isolated database/CI setup and learner acceptance evidence in
slices 17-20. Nothing pushed. Continuing with optional confidence-skip navigation
as a separate, uncommitted slice.

### Checkpoint 21 (in progress): optional confidence-skip HTTP journey

User requested "checkpoint continue". Committed slices 17-20 as `5b78d2c7`
(`Add PostgreSQL and full learner HTTP acceptance tests`); nothing pushed.
Then added a separate learner HTTP case reusing the existing application factory
and isolated-database lifecycle. The fixture now includes a confidence
introduction before the questions.

The test extracts and follows the actual rendered "Skip questions" link, proves
the introduction has not started learning, then reaches learning with a skip
timestamp but no confidence start/completion, answers or assessment. Certificate
access remains denied. Revisiting the introduction/learning and later answering
both confidence questions preserves the original skip and learning-start
timestamps, retains selections and does not complete the module.

Both targeted HTTP journeys passed, followed by the full Release suite with
PostgreSQL enabled: **391 passed, none skipped**. Editor diagnostics passed.
Updated T08/Q03 and testing instructions. No production behaviour/schema changes
or shared development learner updates. This slice remains uncommitted.

### Checkpoint 22 (in progress): conditional registration HTTP acceptance

**User objective:** continue learner-journey acceptance. Added three HTTP cases
using the existing real-MVC/cookie/anti-forgery/PostgreSQL fixture:

- Custom setting and role descriptions reject blank inputs without persistence.
  Valid draft descriptions survive sign-out/in, role whitespace is trimmed and
  summaries encode custom markup. Incomplete registration cannot access training
  or complete; omitted research preference saves neither preference. Explicit
  false/false answers and remaining experience allow registration to complete.
- Reviewing an England/custom-role draft into Scotland/training-provider clears
  stale authority, custom descriptions and experience. Summary omits irrelevant
  rows; direct GETs and POSTs to skipped steps cannot restore those answers.
- Starting in Scotland with an authority/no-role setting skips authority/role.
  Reviewing country to England requires authority before completion, without
  inventing a requirement for role or experience.

The first two targeted cases passed. The third initially expected review-mode
navigation to survive visiting the summary to test rejected completion. The
existing summary intentionally ends review mode; corrected the test to follow
normal role-skip/preferences navigation afterward rather than changing app
behaviour. All three targeted cases then passed. Full Release with PostgreSQL
enabled: **394 passed, none skipped**. Updated README/A05/A06/Q03 evidence.
No production code/schema changes or shared development learner updates.
Browser UX approval, all setting/role combinations and process/database restart
are not claimed. Changes remain uncommitted; nothing pushed.

### Checkpoint 23 (in progress): account-edit HTTP acceptance

**User objective:** continue learner-journey acceptance. Added two account-edit
cases using real MVC forms, cookie authentication, anti-forgery and isolated
PostgreSQL storage. Setting preview and cancellation leave saved fields intact;
an invalid childminder-role submission leaves all existing setting/authority/
role/experience answers unchanged. A valid save changes them together, then a
no-role setting clears stale answers and direct role edits cannot restore them.

The second case rejects a missing custom-role description, saves a trimmed and
encoded custom description, clears authority on moving to Scotland, rejects a
return to England without authority, then saves a valid return. Missing email
preference does not save. Updating email/research separately preserves the other
preference even when unrelated values are posted. Fresh contexts verify saved
and unchanged values, preserved registration and no invented training progress.

Both targeted cases passed; the full Release suite with PostgreSQL enabled
passed **396 tests, none skipped**. Editor diagnostics passed.
No production code/schema changes or shared learner
updates. Browser/accessibility approval and exhaustive edit combinations are not
claimed. This and slices 21-22 remain uncommitted; nothing pushed.

### Checkpoint 24 (in progress): closure, other sessions and subsequent sign-in

**User objective:** continue learner-journey acceptance. Added an HTTP case
starting with a registered learner, note and earned completion. Invalid closure
reasons cannot save; confirmation without a reason returns to reason selection.
After valid closure, account/log/certificate requests redirect the signed-out
client to sign-in. A second previously authenticated client receives 404 for
those resources because the old subject no longer resolves. Repeated anonymous
confirmation cannot change closure.

Subsequent Development sign-in creates a new unregistered learner with no
restored name, notes, progress or assessments; training/certificate/log routes
require registration again. Fresh contexts verify the original closure timestamp
and redacted subject remain unchanged, notes remain deleted and retained
completed progress/assessment stay attached only to the closed learner ID.
This tests existing behaviour; it does not prescribe a production identity
continuity policy or prove provider-side logout. Stale-session 404 recovery UX
remains an explicit gap.

Targeted case passed and full Release with PostgreSQL enabled passed
**397 tests, none skipped**. Editor diagnostics passed. Updated README/A08.
No production code/schema changes or shared development learner updates.
This and slices 21-23 remain uncommitted; nothing pushed.

### Checkpoint 25 (in progress): stale-session recovery

**User objective:** continue learner-journey work. Fixed the stale-session 404
identified in slice 24 using scoped cookie events shared by Development and OIDC
cookie sign-in. Signing in stamps the server-resolved learner ID; validation
requires that ID and subject to resolve to the same active learner. Invalid
sessions are logged without identity data, rejected and signed out locally;
normal authentication challenge handles protected-page recovery. Database errors
are not swallowed or mistaken for missing accounts.

The learner ID is necessary because closure renames the old subject and later
sign-in may create another account under the original subject. Checking only
subject would allow a dormant old cookie to attach to that new account. Extended
the closure HTTP test to verify cookie deletion, sign-in redirects and rejection
of a third dormant client after a fresh account has been created. Active session
and complete learner journey coverage still passes.

Intentional compatibility change: existing cookies without the new claim require
a fresh sign-in. This performs an account read per authenticated cookie request.
No database migration or identity-provider logout behaviour changes.
Targeted closure/full-journey cases passed; full Release suite with PostgreSQL
enabled: **397 passed, none skipped**. Editor diagnostics passed.
README/A08 updated; real One Login and browser recovery acceptance remain.
Changes remain uncommitted; nothing pushed.

### Checkpoint 26 (in progress): module actions and resume navigation

**User objective:** continue learner-journey acceptance. Extended the full HTTP
journey to extract actual primary action links from the module overview, assert
their labels/destinations and follow them. Checks cover initial Start module,
Continue to saved page notes, Continue to the interrupted assessment after
sign-out/in, Retake after failure, Continue to results after a pass and View
certificate after completion. Verifies course-list available/in-progress,
retake/result actions and completed/all-completed states at matching stages.

The fixture intentionally exercises the first-question retake fallback because
it has no assessment introduction; focused existing tests cover introduction
rules. No new claims about CMS-wide navigation or direct-URL authorization.
Targeted journey and full Release suite passed: **397 tests, none skipped**
with PostgreSQL enabled. Count unchanged because this extends an existing case.
Editor diagnostics passed. Updated README/T03. No production changes, no shared
learner writes; changes remain uncommitted and nothing pushed.

### Checkpoint 27 (in progress): deferred feedback continuation HTTP acceptance

**User objective:** continue core learner acceptance. Extended the deterministic
module with consecutive deferred feedback pages between results and thank-you.
The full learner HTTP case now follows results to feedback, checks explicit
unavailable/no-submission messaging and hidden CMS question content, extracts the
rendered continuation link and follows it to thank-you and the actual PDF.
Fresh reads before continuation prove feedback viewing has not completed the
module or saved a feedback answer; certificate access remains blocked until
the existing thank-you completion step.

Added a negative HTTP case for direct feedback access without an assessment pass.
It checks the continuation skips consecutive feedback pages, no question controls
or answer endpoint are rendered, a forged feedback submission is rejected,
completion returns a pass-required notice, and PDF access remains blocked.
Fresh context checks show only the visited feedback page, not skipped pages or
thank-you, and no fabricated start/completion/responses/assessments/notes.

Both targeted cases passed; full Release suite with PostgreSQL enabled:
**398 passed, none skipped**. Editor diagnostics passed. Updated README/T10.
No production changes, no actual feedback feature introduced, no shared learner
updates. Terminal feedback fallback retains focused existing coverage; human UX
and browser interaction acceptance remain outstanding. Changes remain
uncommitted; nothing pushed.

#### Checkpoint commit for slices 21-27

User requested "checkpoint". Re-ran the full Release suite with PostgreSQL
enabled: **398 passed, none skipped**; whitespace checks passed.
Checkpoint commit: `Strengthen learner journey acceptance and stale-session recovery`.
Includes optional confidence skip, conditional registration, account edits,
closure and subsequent sign-in, learner-record-bound cookies, rendered resume
actions and explicit deferred-feedback continuation evidence.
Existing cookies without the learner ID claim require fresh sign-in.
No database migration. Nothing pushed; local app remains running.

### Checkpoint 28 (in progress): name-form keyboard error recovery

**User objective:** continue, moving into targeted browser interaction checks.
The shared browser initially had an older session; used Development sign-in,
then submitted empty name fields only. Observed inline errors without an error
summary or associated error descriptions. No valid name edit was submitted.

Updated the shared registration/account name form with friendly validation
display names, Error-prefixed title, labelled/focusable autofocus summary with
links ordered by field name, and aria-invalid/aria-describedby on invalid inputs.
Error text remains Razor encoded. No JavaScript dependency added.
Added HTTP coverage of both name routes and unchanged persisted names.

Targeted regression and full Release suite with PostgreSQL enabled passed:
**399 passed, none skipped**. Editor diagnostics passed. Restarted the local app;
browser confirmed the summary receives focus and Tab/Enter navigates its error
link to the input. Returned to My account and confirmed the saved name unchanged.
Only sign-in and rejected edits were performed on the shared learner; no saved
profile/training edits. Other forms and full screen-reader/accessibility review
remain outstanding. Updated README/acceptance evidence; changes uncommitted.

#### Checkpoint commit for slice 28

User requested "checkpoint and continue". Checkpoint commit:
`Improve name form validation and keyboard error recovery`.
Final full Release validation: **399 passed, none skipped**, PostgreSQL enabled.
Whitespace checks passed. Includes shared name-form accessibility changes,
HTTP regression and browser evidence. Nothing pushed. Continuing separately.

### Checkpoint 29 (in progress): preference-form error recovery

User requested "checkpoint and continue". Committed slice 28 as `dde7cd82`
(`Improve name form validation and keyboard error recovery`); nothing pushed.
Then improved the shared account email/research preference form with a labelled,
focused and linked error summary, Error-prefixed page title, and invalid state/
error descriptions on both radio choices and the fieldset. Existing valid-save
behaviour and headings are unchanged.

Added HTTP coverage of missing selections on both routes and fresh-context
assertions that neither saved preference changed. Targeted validation/edit
cases passed; full Release suite with PostgreSQL enabled: **400 passed, none
skipped**. Editor diagnostics passed.

Restarted the local app and checked the shared browser. To exercise the missing
selection case on a prefilled edit form, cleared only the in-browser radio
selection using Playwright before submitting; the rejected form focused the
summary. Tab/Enter navigated its link to the first radio. Returned to My account
and verified email/research preferences were unchanged. No valid edit submitted.
This is targeted browser evidence, not a screen-reader/accessibility sign-off.
Updated README/acceptance; this slice remains uncommitted.

### Checkpoint 30 (in progress): location-form error recovery

**User objective:** continue targeted learner form accessibility. Updated account
location with Error-prefixed title, focused/labelled linked error summary and
invalid/error associations on country/authority selects. Authority keeps its
existing hint alongside the error description. Unknown/model-level errors remain
visible without fabricated field links. No validation or persistence rules changed.

Added HTTP coverage of invalid country and authority, unchanged saved account
fields, and absence of invalid state on a normal GET. Targeted cases passed;
full Release suite with PostgreSQL enabled: **401 passed, none skipped**.
Editor diagnostics passed. Restarted local app and submitted only an invalid
missing-authority edit in the shared browser. Summary received focus; Tab/Enter
focused the authority control with both hint/error associations. Returned to My
account and confirmed England/York unchanged; no valid profile edit submitted.
Updated README/acceptance; this and slice 29 remain uncommitted. Nothing pushed.
This is targeted evidence, not full accessibility sign-off.

#### Checkpoint commit for slices 29-30

User requested "checkpoint". Checkpoint commit:
`Improve preference and location form error recovery`.
Full Release validation: **401 passed, none skipped**, PostgreSQL enabled.
Whitespace checks passed. Includes preference/location accessibility changes,
HTTP regressions and targeted keyboard browser evidence; saved learner details
unchanged. Nothing pushed. Local app remains running.

### Checkpoint 31 (in progress): role/experience error recovery

User requested "continue" after checkpoint `e1fdb9b5`. Added an Error-prefixed
title, labelled/focused linked summary in visible field order, and invalid/error
associations to the account role/experience form. The custom-role input retains
its hint alongside errors. Added friendly required-field messages; reference
validation no longer adds a second error when that field already failed model
validation. Valid-save and atomic persistence rules remain unchanged.

Added PostgreSQL HTTP coverage of empty/invalid choices, missing custom-role
description, ordered links, error associations, unchanged saved answers and
normal GET state. Targeted tests passed; final full Release suite with PostgreSQL
enabled: **402 passed, none skipped**, including twenty integration cases.
Restarted the local app and verified browser summary focus, Tab/Enter navigation
into the role select and unchanged saved role/experience after rejected edits.
No valid shared profile edit submitted. Health and database cleanup checks passed.
Updated README/acceptance. This slice is uncommitted; nothing pushed. This is
targeted browser evidence, not assistive-technology/accessibility sign-off.

### Checkpoint 32 (in progress): non-development authentication boundary

User requested "continue" after the status review recommended a bounded
acceptance pass rather than further form-by-form polishing. Added six HTTP cases
using real middleware/controllers in Production and Staging. A valid anti-forgery
Development-sign-in POST returns 404 without a session cookie; missing-token POST
returns 400 and GET returns 405. Subsequent protected account access remains
anonymous and challenges at the provider. Normal sign-in checks the OIDC code
challenge, PKCE S256, state, nonce, scopes and callback.

Test-host settings must be supplied before minimal-host startup validation.
Static OIDC configuration is installed through ConfigurationManager so challenge
tests do not fetch remote discovery metadata. An initial configuration-only
override left the default discovery manager active; corrected the test setup.
No application authentication behaviour was changed. No real credentials,
private key, database or CMS access is needed for these six cases.

Targeted cases: **6 passed**. Full Release suite with PostgreSQL enabled:
**408 passed, none skipped**, including twenty PostgreSQL cases. Editor diagnostics
passed. Updated A03 to Implemented for this bounded requirement; A01/A02 and
security sign-off remain outstanding. Real One Login exchange, callback and
logout are not verified by static challenge tests. Final repository inspection
found slice 31 had been committed separately as `69fac47e` during this work.
Slice 32 remains uncommitted. No commit or push was performed by this slice.

#### Checkpoint commit for slice 32

User requested "checkpoint and continue". Saving the six non-development
authentication HTTP cases and related acceptance documentation. Full Release
validation: **408 passed, none skipped**, PostgreSQL enabled. Whitespace checks
passed. No push performed. Continuing with failed/cancelled sign-in recovery.

### Checkpoint 33 (in progress): cancelled/failed sign-in recovery

User requested "checkpoint and continue". Saved slice 32 as `0be51bcb`
(`Verify non-development authentication boundaries`); no push performed.
Added explicit OIDC access-denied/remote-failure handling and a no-store local
recovery page offering retry/home links. Events log failure category/request trace,
not raw provider error descriptions or tokens. No exception is treated as a
successful sign-in, and no caller-supplied return URL is used.

Eight real-middleware HTTP cases cover cancellation, provider errors, invalid
state and absent correlation cookies in Production/Staging, with static discovery
metadata. They verify recovery rendering, no reflected protocol/return-URL data,
no session cookie, continued anonymous protected access and a fresh retry state.
Targeted authentication tests: **14 passed**. Full Release with PostgreSQL:
**416 passed, none skipped**, including twenty PostgreSQL cases. Editor diagnostics
passed. Restarted local app; browser confirmed recovery heading and retry/home
destinations. This browser check directly visited the recovery page, not a real
provider callback; the shared learner session and saved data were not changed.

Updated A02 to Partial: real provider exchange/failures, missing-claim handling
and expired-session acceptance remain. No security/accessibility sign-off claimed.
This slice remains uncommitted.

#### Checkpoint commit for slice 33

User requested "checkpoint continue". Saving cancelled/failed sign-in recovery
and eight callback cases. Full Release validation: **416 passed, none skipped**,
PostgreSQL enabled. Whitespace checks passed. No push performed. Continuing with
expired-session recovery and persisted learner data.

### Checkpoint 34 (in progress): expired and sliding session acceptance

User requested "checkpoint continue". Saved slice 33 as `39a511ac`
(`Add cancelled and failed sign-in recovery`); no push performed.
Added two isolated PostgreSQL HTTP tests with a test-only cookie TimeProvider.
Application code, one-hour lifetime and sliding-expiration settings are unchanged.

The expiry case inspects the actual issued ticket's one-hour lifetime, checks an
active request before half-life does not renew, advances beyond expiry and verifies
account/log/question/certificate reads and a previously opened name POST require
sign-in. Reauthentication uses the same learner and restores saved notes and
incomplete assessment answers; saved profile, start time and completion state
remain intact. The renewal case advances beyond half-life, captures the renewed
cookie, then proves the original cookie expires while the renewed session works.
Avoided the exact half-life boundary because serialized ticket timestamps round
to seconds; tests use 29/31 minutes to exercise either side deterministically.

Targeted cases: **2 passed**. Final full Release suite with PostgreSQL enabled:
**418 passed, none skipped**, including twenty-two PostgreSQL cases. Editor
diagnostics passed. This uses real cookie middleware, MVC and EF stores, not a
mock authentication handler. No shared learner data or runtime settings changed.
Updated README/A02 evidence; real One Login reauthentication/provider-session
expiry and browser acceptance remain outstanding. This slice is uncommitted.

#### Checkpoint commit for slice 34

User requested "checkpoint". Saving expired/sliding session HTTP acceptance
and related documentation. Latest full Release validation: **418 passed, none
skipped**, including twenty-two PostgreSQL cases. Whitespace checks passed.
Application session configuration and shared learner data are unchanged.
No push performed.

### Checkpoint 35 (in progress): local and provider logout acceptance

User requested "continue" after slice 34 checkpoint `490df305`.
Added two PostgreSQL HTTP cases. Development logout rejects GET and missing-token
POST without ending the active session; valid POST deletes the cookie and
protected access challenges again. Reauthentication preserves profile/notes.
Production uses a test-issued protected existing-session cookie, real validation/
account lookup and static discovery metadata, without enabling Development sign-in.
It rejects a missing-token POST, then verifies valid-POST cookie deletion,
provider end-session URL/state/callback and protected access after logout.

The existing provider callback returns to `/account/sign-in`, which immediately
starts another provider challenge. Recorded this as an outstanding UX decision,
not silently changed or treated as an approved logout landing. Real provider
acceptance (including required logout parameters) remains unverified.

Targeted cases: **2 passed**. Full Release with PostgreSQL enabled:
**420 passed, none skipped**, including twenty-four PostgreSQL cases. Editor
diagnostics passed. Test-only changes; no application behaviour, runtime settings
or shared learner data changed. Updated A01 to Partial with explicit simulated/
real-provider evidence boundary. This slice is uncommitted.

### Checkpoint 36 (in progress): explicit local One Login testing

User asked how to test One Login locally and challenged the web-search tool's
authentication prompt. No further web-search tool used. Added an explicit
configured-One-Login link alongside the Development test-user button and a
challenge route that is unavailable when OIDC is not registered. Production's
existing automatic sign-in challenge is unchanged. Documented local user-secrets,
test-client/public-key registration, exact HTTPS callback, separate learner
identity and Development's local-only logout limitation. Localhost acceptance
must be confirmed with the team's provider administrator; not assumed.

Targeted authentication/smoke tests: **21 passed**. Editor diagnostics passed.
Full Release attempt: **398 passed, 24 failed**; every PostgreSQL case failed
because localhost PostgreSQL now requires a password absent from the previous
test connection. Unix socket connection also unavailable. No database settings,
credentials or running app were changed to work around this environment change.
Last all-green full suite remains 420 from slice 35, not 422. New code is built
and targeted tests pass; database-enabled regression rerun remains blocked.
This slice and prior logout acceptance changes remain uncommitted.

### Checkpoint 37 (in progress): restore local database connectivity

User reported the browser error on sign-out. Confirmed cookie validation failed
because port 5432 was now served by the Rails `recovery_db` container rather than
the stopped Homebrew PostgreSQL cluster. Did not change container credentials,
stop Rails containers or recreate databases. Started the original Homebrew cluster
on free localhost port 5433 and persisted that cluster's port via ALTER SYSTEM.
Updated the VS Code app task and documented test connection to use 5433.

Restarted app using the corrected task. Browser account page restored the existing
learner, and actual Sign out/local Sign in both succeeded without profile changes.
Full Release rerun with PostgreSQL on 5433: **422 passed, none skipped**, including
twenty-four PostgreSQL cases. This resolves slice 36's blocked validation; no real
One Login exchange is claimed. Shared browser returned to My account.

### Checkpoint 38 (in progress): web-host reconstruction acceptance

User requested "continue". Added an isolated PostgreSQL HTTP case saving notes
and a multi-select assessment answer through real forms, then disposing the first
web host/client before creating a second. A test-specific persisted data-protection
key directory allows the original cookie to authenticate against the second host.
The profile/note/selected answers remain, and submitting the remaining question
completes the same assessment attempt without duplicate learner/attempt records
or premature module completion. Start timestamp remains unchanged. Key files are
cleaned after both hosts stop; shared learner/runtime data is untouched.

Targeted case: **1 passed**. Full Release on PostgreSQL port 5433:
**423 passed, none skipped**, including twenty-five PostgreSQL cases. Editor
diagnostics passed. This is sequential in-process host reconstruction, not an
OS-process or PostgreSQL restart and not deployment key-storage validation.
Updated acceptance/README; earlier uncommitted changes retained.

## Future entry format

At subsequent checkpoints, append:

- Next sequential checkpoint number, user-requested objective and checkpoint commit, if any.
- Changes and significant decisions, including approved behaviour differences.
- Tests actually run and their outcomes; distinguish automated and manual evidence.
- Remaining gaps, blockers, local test-data changes and the next step.

Keep entries concise and evidence-based. Do not treat generated code, test totals
or this log as proof of human review, complete parity or production readiness.
