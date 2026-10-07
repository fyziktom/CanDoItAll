# SchedulerPlanner — current source, target boundary and consequences

This is the selected next module after Plugins corrections. References such as SC01 resolve
in [SOURCES.md](SOURCES.md). Findings are based on inspected code at the reviewed SHA, not
fresh application execution. Complete the omitted runtime/consumer inspection before edits.

## Why this module now

The `/scheduler` route is a cohesive complete workspace with four tabs and bounded owner
operations. Its 114,778-byte Razor page still directly depends on the scheduler, CRON,
Workflow schema/options, authority factory, Agent workspace/launcher and browser services.
Its module references AppComponents, AgentFramework components/core/tooling, Workflow runtime,
Infrastructure and Quartz hosting (SC01, SC04). It therefore offers a meaningful standalone
UI development target, without requiring a rewrite of Workflows or fire admission.

The current UI index lists the completed Plugins seam alongside prior independent surfaces
(EV05). This is a bounded next-step choice, not a claim to have re-audited every remaining
module or to rank every repository subsystem by effort. Larger runtime-coupled Workbench,
Processes and Workflow UI work is not part of this assignment.

## Rendered inventory

| Surface | Existing behavior to retain | Boundary consequence |
| --- | --- | --- |
| Header / context | Counts, Refresh, exact managed Scheduler Agent avatar/action, source context provider | Host supplies contextual Agent behavior without injecting its runtime into light UI |
| Calendar | Actual CanvasCalendar, view/timezone/selection, planned events and recent execution overlay, double-click edit | Rendering and browser interaction can move; recurrence projection/launch stay owner-side |
| Schedules | Search, target/state filters, cards, New, Edit, Pause/Resume, Delete | Typed plan action identity and operation conflict admission |
| New schedule | Name, exact target/version, CRON presets/description, timezone, misfire, typed input, raw JSON, enabled, Save/Reset | Stable draft and frozen submitted payload across all awaits |
| History | Bounded search/status/kind query, paged display, route/retry/result state | Pure read; never execution retry disguised as refresh |
| Target picker | Card list, target name/status/tags, search/type/tag filters, new/edit modes | Capture origin editor/version; stale selection cannot bind another draft |
| Edit dialog | Name/description/CRON/zone/misfire/raw JSON/enabled plus saved context and Change/Delete/Cancel/Save | Late saved result cannot dereference a closed editor, close a successor or reset later input |
| Delete dialog | Exact selected plan and confirmation | Correct wording to real persistence; keep retained fire-admission identity |
| Assets | Page-scoped CSS, calendar-specific JS, BaseLib/CanvasLib/font/static assets | Move DOM scoping and JS ownership; verify source and published hosts |

The code is not already a clean renderer simply because some shared controls are components.
Inspect the complete child closure and public type graph, including source-mode sibling
replacement. A backend-free sandbox must use the actual calendar, not a fake thumbnail.

## Source-level risks to resolve during extraction

### SC-R1: mutable commands and unsafe post-await UI effects

SC01 `SaveScheduleAsync` (around 1199 onward) awaits input validation and operator authority,
then submits the mutable page `editor`, ignores the returned summary and subsequently reloads,
replaces the editor and switches tabs. `SaveEditScheduleAsync` (around 1311 onward) uses the
mutable `editScheduleEditor` before and after awaits. Header/dialog dismissal may retire or
null it while a save is pending. A successor dialog can also be affected by late completion.

The global `RunUiOperationAsync` (around 2107 onward) only protects error/final completion
assignment. It does not reject direct duplicate commands or fence every assignment inside
its delegate. Keep the existing more targeted schema/options/edit-load generation protection,
but do not treat the global busy counter as an ownership or commit protocol.

Required outcome: origin-bound stable drafts, immutable whole submission before awaits,
correct input/authority/version binding, actual same-target admission, committed ID adoption,
field-aware reconciliation and effects scoped to the still-valid view. Newer edits can remain
in an editor; close/reset after save is appropriate only if it still represents the submitted
unchanged draft. Readback failure never turns an accepted create into a fresh create.

### SC-R2: persisted plan mutation precedes projection/reload/log completion

SC03 SavePlanAsync persists/commits and only afterward calls SynchronizePlanAsync, ReloadAsync
and logging. SetPlanEnabledAsync and DeletePlanAsync also save before synchronization.
These are multiple completion stages. A generic failed Task does not prove “nothing saved”.

Add a narrow honest receipt path for exact known mutation/ID versus failed follow-up. Reuse
the existing owner semantics and source lease/transaction order, and adapt directly affected
Agent/API consumers intentionally. UI-only guesses are not receipts. Unknown commit outcomes
remain unknown. Refresh is read-only and cannot recreate plans, execute historical fires or
silently grant fresh authority. An explicit exact-plan projection-repair action, if needed,
must use the existing current-state owner and be distinguished from read retry.

The bundle does not mandate a durable operation journal, new optimistic concurrency layer,
outbox, database schema or general scheduler framework. Tests must exercise actual PostgreSQL
commit + controlled projection failure to substantiate the new result classification.

### SC-R3: invalid advanced JSON can be silently overwritten

SC01 `ParseWorkflowInputJsonObjectOrNew` catches parsing failure and returns an empty object.
`SynchronizeWorkflowInputJson` then applies typed fields/defaults and overwrites raw JSON;
Save invokes this before validation. Non-object/malformed user content can disappear rather
than remain actionable invalid input. Preserve the raw draft and reject unsafe conversion.

Keep unrepresented valid properties, numeric types and existing supported JSON-path behavior.
Late normalized validation must not replace a newer raw edit or different Workflow version.
Typed and advanced forms share one deliberate ownership policy, not mutually overwriting
mutable buffers. Defaults are submission/schema semantics, not permission to erase raw text.

### SC-R4: per-input option fan-out and request freshness

SC01 typed controls call the same async handler on input and change; the handler lists options
for every schema parameter after each value change. This is a performance risk, not a measured
latency claim. Preserve current dependent-key clearing semantics where correct while reducing
redundant owner work: avoid unchanged blur duplicates, load affected sources only, coalesce
in-flight reads and fence their origin. Raw drafts must remain immediately editable while
options are unavailable. No unbounded cache or cross-profile data sharing.

The current history query defaults to Take=50 and owner clamps 1..250. SC03 projects up to
18 occurrences per enabled plan in a 30-day horizon, includes queried history from the last
14 days and caps combined calendar events at 160. Preserve/characterize these bounds and
make counts honest; they are not a complete activity history. Do not incidentally remove
bounds or promise all events will be visible in large-catalog fixtures.

### SC-R5: one document-global JavaScript registration

SC05 stores a single `activeBinding`. `attachCalendarDoubleClick(host, reference)` detaches
any prior binding, listens on document and accepts any matching scheduler canvas; it never
checks containment in the supplied host. Another mounted surface can steal the first surface's
callback, and detaching one can remove the other's registration.

Give each actual host a scoped, idempotent binding/disposal. Retain actual event→plan semantics,
prevent duplicate native/JS dispatch, protect tab unmount/remount and late import/interop.
Use a real two-host browser test and mouse input; a static string assertion is insufficient.
Moving SC06 CSS requires checking generated scope selectors and the changed descendant DOM.
Keep authoritative theme assets without a Web project-reference shortcut.

### SC-R6: delete confirmation contradicts display history cascade

SC01's dialog says “Existing run history is not edited by this action.” SC02 configures
SchedulerPlanRun→SchedulerPlan with DeleteBehavior.Cascade. SC07 explicitly preserves that
Plan/Run cascade and separately retained FireAdmissions. Change the UI explanation to the
verified storage semantics; do not change migrations/cascade to preserve inaccurate copy.
Confirm that the exact retained admission identity survives delete and cannot be erased by
a recreated plan. Do not claim this deletes actual Workflow owner runs unless that independent
owner contract is proven to do so.

## Ownership, contract and dependency decisions

| Responsibility | Current source | Desired ownership |
| --- | --- | --- |
| Plan CRUD/read/default and calendar projection | ISchedulerPlannerService / SchedulerPlannerService | Existing module owner; presentation calls narrow in-process port |
| Form values / validation display / selection / modal state | Razor page | Per-view presentation policy and stable drafts |
| Pure summaries/query/enums/schema descriptors | Mixed SchedulerPlannerModels.cs | Light contracts or existing appropriate model library |
| EF entities/mappings, authority JSON, fire data | Same Models file and scheduler runtime | Implementation; never copied into UI contract project |
| Local authority capture | IWorkflowStructureAuthorityFactory | Production host maps exact submitted intent, not operator-editable UI data |
| Source lease / transaction-enlisted policy | Scheduler/Workflow runtime | Preserve ordering and access ceilings; review/test affected contracts |
| Agent context | SchedulerAgentChatContextBuilder / actual provider and launcher | Real host integration through light slot/view; harmless fake in sandbox |
| CRON and target/input validation | Existing owner services | Canonical owner semantics; no new renderer parser/authority |
| Calendar DOM/JS/CSS | Page and module static web assets | Real renderer's local browser responsibility |

An extra shared Presentation assembly is optional, justified only by concrete reuse. Prefer
that over duplicating production policy in the scenario fake. Keep the production route a
thin host while retaining explicit authority and external effects. Do not make rendering
responsible for executing or coordinating the scheduler runtime.

Source SC09 includes Agent view tokens Calendar/Schedules/NewSchedule and existing context
facts/permission presentation. Preserve compatibility; the UI History tab does not authorize
a new Agent protocol. Keep current completion-refresh behavior and recheck access readiness;
context decoration is not command authorization.

## Consequence and expansion map

| Change | Required consequences | Not automatically authorized |
| --- | --- | --- |
| Pure model relocation | Update project graph, namespaces/serialization/forwarding where needed, Agent/owner/test consumers, solution and actual CI selections | Move every scheduler/domain type or alter JSON/wire defaults |
| Narrow stage-aware mutation result | Test actual durable boundary + sync/reload/log failures; adapt affected host/Agent/API handling | Generic transaction/retry framework; claim exactly-once |
| New renderer/draft policy | Characterize both create/edit; source-bound slots; update real page tests plus light tests | Replace BaseLib/CanvasLib or rewrite all UI modules |
| Scoped CSS / JS move | Source + publish path tests, actual computed style and two-host event routing, watch measurements | Stale copied assets; whole-Web dependency solely for CSS |
| Agent integration seam | Exact managed identity, current source/selection/overlay/readiness/completion, relevant context/admission tests | Give sandbox/renderer privileged authority or broaden scope |
| Text correction for deletion/Process support | Verify real behavior and update focused UI expectations | Schema changes, Process launch, history/fire-retention redesign |
| Performance cleanup | Owner-call budgets, measured render/edit costs, bounded reads/caches | Speculative global caching or full application optimization |

Preserve hidden StartAtUtc/EndAtUtc and any real existing settings when forms round-trip.
Persisted Workflow versions and authority snapshots are not normalized to “latest”. Do not
change the scheduled fire's original correlation or authorizer ceiling. Current source
policy leases precede SQL; provider execution occurs after locks/leases are released.

## Entry discovery obligations

SC08 is only a partial inspection of the existing large SchedulerPlannerPageTests file.
The inspected cases cover managed Agent opening/context, real tabs/calendar, history route
labels, picker filters and typed/raw JSON synchronization. Find and retain the rest of that
file and all owner/schema/options/Agent/Quartz/retained-admission tests using current symbols.
Do not fabricate counts or assume the existing namespace identifies only Process behavior.

Current dispatcher/admission/transfer implementation was not fully audited in this review.
Read its exact impacted portions before touching contracts, and expand proof according to
actual changes. Its deferred native-output/legacy-reconciliation/profile-transfer obligations
remain separate work; they do not prevent a complete safe UI seam and cannot be labeled
solved simply because a sandbox renders successfully.
