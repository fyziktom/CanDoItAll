# Runnable candidate, freeze and recovery

The customer goal is an application the user can start and exercise on Wednesday, not merely
new source files and a terminal saying tests passed. Deliver the exact candidate and its data.

## Baseline before further edits

Build the changed native host using current repo dependencies. Publish to a versioned owned
output outside source/bin folders, with private configuration. Start against an owned PG18
server/database and storage root, wait for actual application readiness, open /agents, Projects
and a real Structure route, stop gracefully and start again. Record exact commands, endpoints,
source/dependency commits, assembly/static-asset hashes and ports. Preserve this baseline and a
recoverable data snapshot before new integration edits. Never use the ordinary port5032 app
or its existing databases for destructive tests.

If the baseline is not runnable, diagnose/fix that first and preserve evidence. No new UI
extraction should conceal a missing startup dependency, schema/config issue or unavailable
provider. Reuse installed tools and current migrations; do not upgrade SDK, PostgreSQL, MAF
or process schemas opportunistically for this demonstration.

## Candidate deliverables Codex must create and actually test

Produce a small owned directory, for example `artifacts/workbench-completion-wb6/demo-candidate`:

- versioned application publish or immutable image plus the exact source/dependency record;
- tested `Start-Demo.ps1`, `Stop-Demo.ps1`, `Check-Demo.ps1` (or current equivalent scripts),
  using actual supported settings and exit handling, not guessed environment-variable names;
- private configuration path/secure-secret references and a sanitized template without keys;
- owned database/storage snapshot, accepted artifact IDs, and tested restore to a *separate*
  scratch target proving schema and data are recoverable;
- concise Czech `DEMO_RUNBOOK_CS.md` with exact start steps, URLs, project/agent/workflow/process
  choices, prompts, the verified input/output IDs, known limitations and recovery actions;
- `candidate.json` plus readiness/evidence below. Keep public scripts/docs in maintained repo
  locations when reusable; do not commit binaries, credentials, full transcripts or demo DBs.

Start/stop scripts identify their own process/container and must never kill unrelated dotnet,
Postgres or browser processes by name. Detect occupied ports rather than replacing the user's
app. Confirm readiness by HTTP and visible app state, not process existence. Stop preserves
data and accepted artifacts. A dependency/credential preflight produces useful redacted errors.

The preferred demonstration mode uses the production published app without hot reload. A fresh
restart must not rely on an IDE process, leftover scoped service, private fixture seeding side
effect or the developer's open shell. Rehearse the exact start route the user will use. Native
host and Docker may be separate proof lanes; do not claim both when only one was exercised.

## Freeze and controlled fixes

After completing Workbench and passing targeted/native checks, freeze the production source
pair and publish. Run final practical journeys on those bytes. Record before/after binaries
and asset manifests. Later test-only changes go to a separate build configuration; they cannot
retroactively alter the frozen run. A production fix invalidates affected evidence and requires
rebuild plus the affected scenarios and whole-candidate startup/restart smoke again.

One planned broader Stable checkpoint may run for this whole-Workbench/customer candidate,
not after every component. Keep enough attention for actual UI and artifact rehearsal; do not
let repeated broad runs replace customer-path validation. Retain original failed aggregates
and explicitly scoped follow-ups. Known unchanged synthetic security controls are documented,
not deleted or turned green. Any new real secret or product-authority finding blocks readiness.

After candidate validation, stop optional refactoring. Do not begin Processes product UI,
redesign architecture, add responsive layouts, or tune unrelated tests. Leave the signed
candidate, snapshots and concise report for Wednesday preparation. A serious unresolved issue
must be stated accurately and the last safe candidate preserved, not hidden behind a success
flag or a partially completed source checkout.

## Separate readiness statements

Record independently:
1. `workbench_ui_complete`: no unfinished/unclassified active Workbench renderer; native owners
   and legacy classifications justified; dependency guards and publish proof pass.
2. `candidate_runnable`: exact production bytes successfully started, stopped and restarted
   with the preserved intended data, scripts and dependency inputs.
3. `deterministic_integration_passed`: the declared native/browser failure and success lanes.
4. `genuine_model_rehearsal_passed`: actual non-scripted model artifact/workflow/process journeys.
5. `demo_ready`: all mandatory rehearsal paths on the final candidate passed, recovery works,
   and no unresolved presentation-blocking or safety/data issue exists.
6. broad-suite result and exceptions: report separately; never relabel a failed aggregate.

A native content permission check failing as intended is not a demo failure. A broken customer
journey, missing real-model run, wrong artifact, unhandled circuit failure or lost accepted
identity is. A scripted fallback is clearly labelled as prepared demonstration material and
cannot justify `genuine_model_rehearsal_passed` or `demo_ready`.
