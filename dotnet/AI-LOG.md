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

#### Introduction of this log

The user noted that an AI log should have been kept. This retrospective was added
from available evidence, explicitly labelled rather than presented as a
contemporaneous record. The README links to it.

**State at creation:** latest runtime checkpoint `1dbb8b61`; acceptance inventory
and this documentation update are not yet committed. Nothing has been pushed by
the assistant. Latest reported runtime suite: 173 passing Release tests.

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

## Future entry format

At subsequent checkpoints, append:

- Next sequential checkpoint number, user-requested objective and checkpoint commit, if any.
- Changes and significant decisions, including approved behaviour differences.
- Tests actually run and their outcomes; distinguish automated and manual evidence.
- Remaining gaps, blockers, local test-data changes and the next step.

Keep entries concise and evidence-based. Do not treat generated code, test totals
or this log as proof of human review, complete parity or production readiness.
