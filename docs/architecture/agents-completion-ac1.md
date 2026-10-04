# Agents presentation completion AC1

Current work executes the sealed AC1 package on `components-decoupling`. The entry
main is `dba71b3db4a2ae99b5d837e8df2c9df37dc4af4a`. Components local and remote
development both resolve to `2eccdddd05a9b1b0c90935ddee49dc559fd5eec1`, including
the Tooltip and read-only Canvas repairs. FileTools local is
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`; the independent CI pin is unchanged.
The earlier WF1 report's local-only dependency statements describe its historical
checkpoint. AC1 does not replay or modify those historical fixtures.

## S0 native owner corrections

SCH-R1 reproduced with Quartz 3.13.1, a fresh owned PostgreSQL 18.6 database and the
real projection hosted service. The native dispatcher first produced an Active
Workflow's completed run, dispatch history and observed admission. Restart then
failed in Quartz's first-fire calculation while the plan remained enabled. An
earlier fixture attempt used a Draft Workflow and was refused before dispatch;
that failed prerequisite remains in the external evidence.

`SchedulerPlanProjection` now makes a bounded native projection decision before
the owner removes a healthy runtime job. It uses the installed trigger's computed
first occurrence, existing run history and admission, without a new schema or
dispatcher protocol. Disabled, ready, exhausted and recovery-pending decisions
are distinct. No-next-fire sets the existing nullable next-fire field; enabled
intent, target/version, authorizer ceiling, history and admission remain intact.
Malformed CRON, unknown time zone, inconsistent receipt, cancellation and database
failures remain observable. No exception message is used as a terminal-state test.

Explicit old starts retain eligible missed occurrences and the three Quartz
misfire instructions. Consumed occurrences advance using Quartz time-zone and
end-bound calculations. A delayed IgnoreMisfire receipt can retain a still-missed
next occurrence; its fingerprint, history, target and current schedule eligibility
are checked before using that cursor. Incompatible changed plans require explicit
reconciliation. An unresolved past admission is never replaced by an invented
fire. Its original native recovery path retains its identity. An implicit start
still means current Quartz time; an already-expired implicit end has no eligible
occurrence. No calendar field exists in the current plan or trigger builder, so
this change does not introduce calendar association support.

WF1-R1 reproduced through both native preview entry paths: the canvas returned A's
run ID after B lost its acknowledgement; the page path already returned no ID.
`WorkflowPreviewOwner` now clears only the active attempt's reservation when a new
attempt starts. Historical accepted A remains available. B can supply an ID only
through its own result or native observation exception. Pending/unknown guards,
authority capture, owner retirement and fresh draft versions remain intact.

Validation used SDK 10.0.303 and configuration `AC1`: 32 Scheduler decision/contract
cases, 80 Workflow component/native-owner cases and 60 Scheduler integration cases
passed with zero failures or skips. The latter include two real replacement hosts
over each retained completed-plan database, explicit/implicit start variants,
future neighbors, unacknowledged admission preservation and invalid replacement
without deleting healthy jobs. The first browser-created finite fire and final
application restarts remain required at A5. The Workflow cases retain the six
native full-document round-trip tests and both consumer entry paths.

The full portability scan included new protected files: 15,206 reviewed findings
unchanged, final enforcement passed without baseline-write mode. Raw attempts,
TRX, exact discovery and source/assembly hashes are retained privately under
`artifacts/agents-completion-ac1`; failed attempts are not relabeled.

## Architecture gate at S0

Pass for the bounded owner fixes. The new internal projection value owns only
Quartz scheduling eligibility; existing native services own database reads,
projection mutations and admission/recovery. Preview state remains in its native
owner. No project references, service locator, new partial class, schema, global
retry or shared rendering dependency was added. Pure decision tests use real
Quartz independently of the host; PostgreSQL tests prove native composition.
CodeAnalytics, Components and dotnetwatch MCP methods are unavailable in this
session; current source, evaluated MSBuild, CLI and Playwright are the fallback.

## Remaining AC1 stages

### A1 baseline and current reachability

The maintained [renderer census](agents-renderer-census.csv) starts with 227 current
component entries, including shell contributions, dynamic dialog callers, routes,
descendants and scoped assets. Fourteen retained public or legacy components have
no current product caller. In particular, `ScenarioHarnessPanel` is not made
reachable by the separately active backend ScenarioHarness APIs;
`AgentOverviewUsageList` has test callers only. The current floating contribution
uses `AgentFloatingConversationContent`, not `ContextualAgentWorkspaceWindows`.
The compatibility `/chats` route remains reachable through routing even though it
has no lexical component caller. No dormant public type has been deleted.

The census found the active `AvatarPicker` descendant in both the Agent editor and
Simple Chat definition editor. It belongs to the remaining renderer work; its
upload and generation callbacks require their original editor lifetime. Already
isolated Voice, Floating Settings, provider, definition, conversation and Workflow
surfaces remain the product rendering paths.

Before edits, evaluated restore graphs were captured for 19 protected roots. The
Agents UI closure contains 13 projects and its independent sandbox 16. The source
sandbox was exercised at 1920×1080, scale 1. Three successful existing-page probes
per owned Razor, C# and CSS file were measured: Razor 1652/517/259 ms, C#
350/338/341 ms and CSS 1236/853/955 ms. These are end-to-end edit-to-visible
observations including browser handoff. One earlier C# probe became visible but
its restoration encountered a mapped-file lock; that attempt remains failed and
was followed by byte-exact atomic restoration and three successful probes. A
fresh watch process with warm build outputs reached runtime readiness in 15830 ms.
There is no owned JavaScript in this sandbox/RCL to probe. Final comparative
measurements and published assets remain pending.

### A2 shell and complete Usage family

The actual header/statistics/navigation and explicit defaults confirmation now live
in the existing Agents UI leaf. AgentsHomePage retains exact route tokens, native
tab composition, HR/default-feeding effects and context publication. Shell intents
carry the rendered profile generation and tab; stale commands cannot navigate a
replacement selection. No project reference or application service was added to
the rendering leaf.

All three Usage dialogs now render through the same leaf. Their native public
adapters compose one bounded UsageDetailHost, which owns query equality, frozen
snapshots, error handling and cancellation after reads unwind. Parent lifetime,
profile notifications and authentication-cascade replacement retire the view.
Close works during loading and errors; Retry uses the original resolved interval.
Shared snapshots preserve consumer/provider/model identities and partial, unknown
and unpriced distinctions. Paging and chart construction perform no native read.

The independent `/completion` specimen uses these actual surfaces and production
Parity assets. Large-desktop inspection found and repaired two issues before this
checkpoint: the outer stacks needed explicit stretch alignment, and queued Share
cell templates could read a cleared snapshot while a view retired. All template
values now capture the rendered snapshot, including the denominator. The failed
browser circuit is retained. The repeated two-instance journey passes: closing
the model view leaves the separate provider grid and charts usable; loading Close
and error Retry remain usable with the original accepted window.

Fresh builds passed for the UI, native module and independent sandbox/test owners.
The initial native run passed 101 of 107 cases; six new actor-change harness cases
incorrectly omitted CascadingValue child content on replacement. The corrected
107-case run passed, with no skips. After the browser-derived template repair,
22 independent leaf cases (including both queued-template regressions) and 22
native ownership cases passed on fresh assemblies. Earlier broad topic proof is
retained with its source fingerprint; it is not relabeled as the later source.
The frozen final Stable checkpoint remains pending.

### C# Architecture Gate Result at A2

Status: Pass.

| Severity | Finding | Evidence | Required action |
|---|---|---|---|
| Closed | Deferred cell render could observe a retired snapshot | Failing browser circuit, two retained-template regressions, repeated two-instance browser pass | Keep snapshots captured in render fragments |
| Informational | Native ownership remains outside UI | One per-instance UsageDetailHost and existing AgentsHomePage; no injected services in the new surfaces | Final native consumer campaign remains required |

Dependency direction is unchanged: native hosts consume the existing light UI,
Models/Usage values and shared components. No service bag, locator, new project,
schema or protocol was introduced. The new code-behind is the normal Razor
component pair for a single read lifetime, not a partial runtime-service split.
Independent leaf tests require no product host/database; native ownership tests
exercise real composition. A2 may proceed to runtime/floating completion.

### A3 runtime, floating and active adjunct completion

The eight residual renderer owners now compose actual light surfaces: runtime details,
execution log, context/affinity, floating close choices, projected activity, chat actions,
image attachment input and the complete avatar picker. Native adapters keep policy
redaction, typed stream subscriptions, staging/grants, durable approvals, context bindings
and execution. Runtime/log states are immutable safe values; copying uses the displayed
sanitized message. Context projection captures one binding for both revision and display.

The avatar form uses typed source/generation/validated-upload operations. Each operation
captures its source, request, callback and cancellation scope. Close/reopen and parent
retirement suppress late success and notification. The native Agent editor keys the picker
to its edit session; SimpleChats retains its existing editor-generation key. Native image
validation and paid-provider authority stay outside the renderer. No paid requests ran.

The active Agent switch and thread-history adapters already used neutral renderers, but
their global dialog close was defective under stacking. Two failing-first cases showed
that selecting the first dialog completed the unrelated top dialog. Both now close their
own DialogReference. Stale selection/favorite callbacks retain their original generation;
late favorite readback cannot replace a successor picker. Floating history/close callers
carry their own lifetime through dialog selection. Closing a handle still does not decide
a durable pending approval.

Fresh native production build passed with zero warnings. The final focused native assembly
passed 103 discovered cases, including the existing HostPlatform UTC/poison tests, real
image formatter, standard/floating recovery, activity-stream retirement, stacked dialogs
and new A-B-A context/attachment guards. The independent leaf passed 32 discovered cases
(10 adjunct and 22 shell/Usage). Both runs had zero skips. The first native build failed on
a test dictionary target type and was repaired. The failing-first six-case run retained
three passes, the two product dialog failures and one missing test service registration;
the later pass does not relabel that attempt.

Source Parity browser proof at 1920×1080 scale 1 exercised all seven runtime scenarios,
highlighted original entry, independent views, context changes, close choices and native
InputFile. The actual 645-byte owned JPEG passed through the attachment chooser with
SHA-256 `94C4B46CD9E21BCC89373E6DB3ABDB9EEB2DF66B05DC6E35001FD7B8210530FB`
and decoded at 32×32 in the avatar preview. Bundled avatar assets and both modal actions
rendered correctly; no fresh browser console error occurred. An early scenario assertion
ran before the Blazor state update; it is retained and the corrected helper waits for
accepted rendered state. Browser fixture actions are not native persistence proof.

### C# Architecture Gate Result at A3

Status: Pass for the presentation extraction and bounded native dialog fixes. The existing
UI leaf and sandbox project references are unchanged. New surfaces inject no native
service and do not reference Core/runtime/persistence. Value types retain public
compatibility; typed operation delegates cross only the actual avatar effect boundary.
No generic service bag, schema, admission protocol, copied shared component or new
project was introduced. Existing conversation renderers and native services remain owners.

The preserved Voice/SimpleChats audit, final published sandbox and measured edit loop,
native/two-client campaign, final static gates and signed delivery remain in progress.
No final Agents readiness or product release readiness is claimed. Workbench and Processes
extraction remain outside AC1.
