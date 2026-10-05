# Workbench Planning WB1 — complete the planning presentation boundary

You are implementing the next substantial UI/component-decoupling assignment in CanDoItAll.
Use senior architectural judgment and complete the entire selected family. This is not a
request for a plan alone, a single Calendar wrapper, or a new runtime. First preserve the
completed AC1 work, then implement and validate all WB1 stages below.

## Read and establish the actual state

Read current `AGENTS.md`, `.github/copilot-instructions.md`,
`docs/architecture/ui-component-seams.md`, `docs/testing.md` and `.github/workflows/ci.yml`.
Read relevant current SharedInfo standards/skills when available, the supplied
[shared foundation](shared/README.md), and this package's [scope](SCOPE_AND_ARCHITECTURE.md),
[owner rules](NATIVE_OWNERS_AND_LIFETIMES.md), [source observations](WORKBENCH_SOURCE_REVIEW.md),
[validation matrix](VALIDATION_MATRIX.md) and [execution contract](EXECUTION_AND_CLOSURE.md).
Read the topic documents before changing each family; do not repeatedly reread all history.

Reviewed main: `9969913fe653500a59d24af06482f48b758a5ca6` on `components-decoupling`.
Reviewed Components development: `2eccdddd05a9b1b0c90935ddee49dc559fd5eec1`. Both were remotely accessible.
These identify the review, not required execution checkouts. Record actual branch, HEAD,
dirty state and sibling revisions; inspect intervening diffs. Do not reset, change branches,
replace user work or replay the sealed historical AC1/WF1 instructions.

AC1 is structurally complete for active Agents UI. Its Scheduler restart and Workflow
preview identity fixes are present. The reported Stable result was 16,489 passed / one
historical synthetic-control failure / zero skipped; later dialog proof is separate.
Do not turn that report into a fresh all-green result or re-open Agents by counting Razor
files. Read [AC1_REVIEW.md](AC1_REVIEW.md) and [S0_CONTINUITY.md](S0_CONTINUITY.md).

Arrange existing OpenPGP signing through native pinentry early. Ask the operator to unlock
when necessary, keep the same host user/GnuPG home/agent and persistent shell environment,
and verify each coherent signed checkpoint. Do not request a passphrase in text, disable
signing, push, merge, or remove historical bundles. Follow [signing](COMMITS_AND_SIGNING.md).

## Deliver the complete WB1 slice

### S0 — bounded continuity and inherited pricing regression

Verify the actual AC1 source pair and selected native Scheduler/preview/UI continuity with
fresh narrowly targeted tests; do not rerun the entire unchanged AC1 campaign on entry.
Reproduce **WB1-Q1** before fixing it: a price quote starts for a NotStarted task, the same
editor changes to an execution state that forbids repricing, then the original result
arrives. Its amount/status must not be applied as current. Extend to project/owner changes,
resource/effort/manual-cost changes, A-B-A, cancellation, close/reopen and cached responses.
This is a source-derived defect in the old Workbench surface, not an AC1 regression.
Preserve native pricing revalidation and existing quote laziness/cache behavior where valid.
A small correction may be committed before extraction; its tests survive the move.

### W1 — calendar and coherent planning seam

Map every current caller, child, asset, native effect and public compatibility surface for
this bounded family. Define the smallest useful light data/intent boundary, then extract
the full project Calendar page rendering, selected-event detail and actual calendar controls.
The route, canonical reads, original project admission, view-state persistence, agent context
and linked-artifact navigation remain with native hosts. Preserve the actual read-only
calendar contract, supported views, timezone/state parser and existing list export.

Separate the current synthetic “Calendar boundary validation” demonstrations from production
facts. Move their real specimen composition into the sandbox with explicit synthetic labels;
preserve compatibility routes/test contracts when they are genuine consumers. Do not
implement calendar event CRUD merely because a demonstration sets AllowCreate/Edit/Delete.
Inspect the obsolete ProjectEventsCalendar wrapper and retain its public compatibility
without manufacturing a new active use. See [Calendar](CALENDAR.md).

### W2 — entire Gantt schedule, not just its header

Extract the actual GanttChart, task drag source, all toolbar/summary/error states, title and
schedule editing, dependency add/remove/reconnect, task insertion, row ordering, double-click
entry points and complete Mermaid preview/copy/download plus existing PNG export.
Use the existing native projection and mutation owners. Capture the displayed project
lifetime, task/dependency occurrences, expected schedules, projection, operation and callback
before awaits; revalidate at actual mutation boundaries. Do not optimistically alter another
view or roll back a successor projection. Preserve original agent Gantt observations without
turning read-only context into authorization. See [Gantt](GANTT.md).

### W3 — both task form families and their actual children

Complete Gantt task create/edit and general Structure task create/edit presentation, including
estimate, execution state, optional resource/assignee picker and quote preview. The general
form and Gantt form have intentionally different create semantics and fields; reuse genuine
common children without erasing those differences. Wire both production coordinators and
all existing callers to the real extracted renderers.

Keep direct person/agent assignment distinct from additive Workflow/Process definition
attachment. Preserve multi-assignee restrictions, exact expected assignment revision,
historical cost basis, unknown execution state, repository reference, hidden metadata,
null versus zero and raw invalid input. A quote is not a committed price. Native Save must
still reload authoritative rates and history and honor current permission/admission.

Known committed task data, assignment, pricing, resource attachment and compensation are
separate native phases. Retain exact accepted IDs and warnings before any reload. A failure
after the first phase is not “nothing saved”; a lost acknowledgement does not authorize
blind create replay. Do not add a generic transaction/idempotency framework. See
[task editors](TASK_EDITORS_AND_PRICING.md).

### W4 — production, independent sandbox and closure

Build the extracted family in a database/backend-free sandbox using the same CanvasCalendar,
Gantt, Mermaid, dialogs and input controls. Demonstration flags do not grant production
capability. Provide realistic, loading, failed, partial, stale, restricted, invalid-raw,
unknown-result and two-independent-view scenarios. Verify source and independent published
Parity/Fast assets, actual geometry and repeated edit-to-visible behavior.

Run the [native journeys](APPLICATION_JOURNEYS.md): create a project through existing UI,
edit real tasks and graph relationships through the new forms/gestures, compare canonical
readbacks and unchanged neighbors, exercise original assignment/pricing conflicts, and use
an actual agent with governed tools over the same project. Verify file content, approvals,
Workflow/History/Usage and preserved Scheduler restart. Use the owned source/two-client
fixture where shared-provider behavior is reached; do not automatically repeat all unchanged
protocol vectors. Rebuild affected images and record exact input fingerprints.

Do not stop after S0, Calendar, one form, a moved code-behind, or a source-only sandbox.
Complete the entire selected family and report the separate gates honestly.

## Architecture constraints

Prefer `src/UI/CanDoItAll.Workbench.Planning.UI` and
`src/Sandboxes/CanDoItAll.Workbench.Planning.UiSandbox`. Introduce a small Workbench.Contracts
or Planning.Contracts project only for a proven shared data boundary. Inspect the real graph
before final placement; a justified split is allowed, a project/interface quota is not.

`ProjectWorkbenchModels.cs` mixes EF entities, mapping configuration, domain records and
service code. Do not move that file wholesale into Contracts. Never expose DbContext,
tracked entities, ProjectStructureAgentContext, service providers or credential payloads as
rendering dependencies. Native hosts may retain existing ownership types; the leaf receives
safe projections and typed intents, not the power to construct source authority.

Keep completed Agents/Workflow/Providers/Workspace/Projects renderers and sandboxes free of
new Workbench implementation edges. Foundation/MAF/neutral Components must not reference the
new product renderer. Use actual shared controls and move the relevant CSS/JS/Tailwind/asset
ownership, not copies. Module headers and adapters can legitimately remain native.

No global Structure canvas, PM summary/activity, runtime launch, process/workflow assignment
UI, file/storage recovery, or Processes extraction in WB1. Those remain working through their
existing hosts. Do not turn a bounded planning slice into a Workbench domain rewrite.

## Tests, evidence and working conditions

For each change build the affected production project, refresh its owning test assembly,
run --list-tests for the exact intended filter, verify nonzero expected discovery, then run
that same selection. Never use stale --no-build results. Preserve failed attempts and
explain runtime theory expansions. Prefer controlled completion barriers to arbitrary delays.

Decide the broad Stable checkpoint once after the real graph/contracts/owner diff is known.
A new shared contract, owner behavior or CI/test registration can justify one frozen final
checkpoint under current policy. Do not run broad Stable per dialog or rerun it solely to
replace an honestly qualified historical title. Later source deltas get explicit fresh proof.
The mandatory portability-static final enforcement is not waived by focused tests.

Use only supported large desktops, primarily 1920×1080 at scale 1. No new small/medium-screen,
mobile, tablet or responsive-breakpoint tuning. Test functional focus/scroll/footer/menu
reachability. Record repeated real dotnet-watch results rather than promising a speedup.

Use owned PostgreSQL18/volumes/containers/ports and harmless fixture data. Never reset retained
manual fixtures or the ordinary app on 5032. External model responses may be scripted;
canonical owners, approvals, tool dispatch and committed bytes must be real. No paid model
calls, live-budget reset, physical microphone dependency, real mail/payments/deployments.
Credentials, source secrets, transcripts, TRX/media and full logs stay in private artifacts.

Repair small reproduced defects with a regression test. Stop only the affected dangerous
path for a newly demonstrated complex authority/schema/durable-protocol issue; retain evidence
and continue independent safe work. Mapping a blocker is not fixing it. Do not trade authority,
assertions or data preservation for passing status.

Make verified signed commits at coherent boundaries: S0, Calendar/seam, Gantt, both task
families, and closure; combine tightly related parts when sensible. The final maintained
record names remaining Workbench cuts and does not claim all Workbench or release readiness.
Use [evidence.json](templates/evidence.json) as an external ledger template. The packaged
validator checks structural consistency only, not that commands genuinely executed.
