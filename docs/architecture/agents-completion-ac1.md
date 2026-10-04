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

The current renderer/caller/asset census, Agents shell and all Usage renderers,
runtime/floating adjuncts, preserved Voice/SimpleChats consumer audit, independent
source/published sandbox and measured edit loop, final native and two-client
campaign, final static gates and signed delivery remain in progress. No Agents
presentation completion, next-module readiness or product release readiness is
claimed at this checkpoint. Workbench and Processes extraction are outside AC1.
