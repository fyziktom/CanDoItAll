# Frozen-checkpoint application regression campaign

Run the main campaign after W1–W3 and the final Workspace inventory are safely closed. Perform focused tests during each stage, and run an early no-send live rehearsal to identify missing infrastructure before the main campaign. Do not stop after one additional Workspace leaf.

This campaign includes all previously refactored UI families and their important integration boundaries. It does not mandate an agent tool for every module. First inventory the actually shipped entry points: use agent/Workflow integration where it exists, and true UI plus owner verification otherwise. Missing functionality is not permission to implement a new runtime feature.

Every case below has a corresponding entry in `campaign-plan.json`. A row is a test group: material subcases must have their own evidence in its group manifest. One screenshot or one trivial happy path cannot satisfy an entire group.

## Required groups

| ID | Surface / scenario | Required independent oracle |
|---|---|---|
| BASE-01 | Preserve current same-target Catalog draft and Storage Selection fixes | Real row/input/parent dialog tests, original callbacks, no silent reload or parent Save |
| WS-01 | Recovery two feeds, exact selection, read-only and stale context | Actual owner records and zero commands on open/refresh; correct empty-page continuation |
| WS-02 | Recovery Workflow prepared output and cancelled-run receipts | Original intent/output fingerprints, native/file/workflow receipts, zero new model/upload dispatch |
| WS-03 | Data Sources CRUD, schema and saved-profile-only actions | Private control-plane/catalog and actual PostgreSQL state; raw/password semantics and exact returned identity |
| WS-04 | Data Sources transfer including partial failure and close/reopen | Exact source/target/groups, per-handler outcomes, unchanged source and no redirected/replayed transfer |
| WS-05 | Activate for restart, startup lock and MainLayout selector | Running A versus pending B, restart to B; effective canonical connection and unchanged instance-local API state |
| WS-06 | Generic fallback and trusted renderer integration | Real owning Workflow/Plugins configuration read-back; mismatch errors do not fall back permissively |
| APP-01 | Startup, health, direct routes, Settings navigation, back/forward and reload | Correct route data, hydrated controls, no unexpected circuit/console/HTTP errors or duplicate effects |
| APP-02 | Core defaults, Secrets, Files and provider history | Normalized owner values, exact references/identities, masked values, explicit retention confirmation, no eager reads |
| APP-03 | API Access administration and sessions | Real private accounts/JWT registration, revoke/reset/disable/delete semantics, fresh access checks, wire compatibility |
| APP-04 | Storage Catalog, picker and Agent persistence/runtime restrictions | Selected IDs stage until parent Save; allowed catalog succeeds, denied catalog/read-only write refused by runtime |
| APP-05 | Resources Registry/Browse/promotion/reopen | Actual governed file/asset bytes and original source/profile/actor; failed reference read does not certify editor readiness |
| APP-06 | Agents conversation, streaming, cancellation and reopened history | Persisted run/proposals/receipts, correct source, no duplicate send; unrelated drafts survive |
| APP-07 | Workflow editor/save/run and artifact output | Exact saved version/input/source; real runtime, executor output, asset/file read-back and UI run detail |
| APP-08 | Scheduler Calendar/edit and actual scheduled Workflow delivery | Actual schedule/fire admission and original Workflow version/input; one effect despite refresh/restart/retry |
| APP-09 | Prompt Gallery create/edit/select and real consumer | Prompt identity/revision/content accepted by an existing agent or Workflow consumer, not just saved row |
| APP-10 | Collaboration Inbox/Threads/Escalations and reply/read | Persisted items/reply/unread plus draft preservation, optional real tool handoff only if actually shipped |
| APP-11 | TestLab plans/cases/evidence/runs and project/party references | Owner IDs/versions and retained later edits; connect an actual recorded run where supported, do not invent a test runner |
| APP-12 | Plugins catalog/install/configuration/grants and safe invocation | Safe fixture package/connection IDs, exact grant, owner result and harmless registered runtime path; no external delivery |
| APP-13 | Memory seven-tab UI, supported query and caller provenance | Real supported provider/owner result, context/receipt origins; unavailable features refuse before dispatch |
| APP-14 | CRM/HR linked project, tasks, staffing and sensitive reads | Actual owner entities and cross-surface consistency; same IDs in Project Structure/Assignments; permissions preserved |
| APP-15 | Cross-view/caller/profile isolation and unknown outcomes | Controlled late completions plus representative actual UI paths; original effect target, correct cleanup and no replay |
| LIVE-01 | Project Structure agent reads and creates/attaches/reads a real file | Real provider use + tool/owner/UI evidence and fresh content hash; see LIVE_AGENT_JOURNEYS.md |
| LIVE-02 | Existing live CRM planner and HR approve/deny journeys | `execution=live` manifests, nonzero journal requests, actual proposal/approval and owner state |
| LIVE-03 | Actual model-backed Workflow/agent integration | Real provider request, original run/version/input and persistent artifact, not a mock/no-op |
| GATE-01 | Product build and full Stable at final frozen checkpoint | Exact current assemblies, discovery/execution reconciliation, raw TRX and all failures retained |
| GATE-02 | Non-quarantined browser suite and final changed journeys | Actual private full Web; live cases separated; source/published independent new sandboxes |
| GATE-03 | Dependency and watch closure | Evaluated, assembly/public and rendered/static-asset graphs; protected old sandbox graphs unchanged |
| GATE-04 | Portability, docs, secret review and owned cleanup | Current no-write enforcement, evidence integrity, safe artifact scan and exact-resource cleanup |

## Module-specific guardrails

**Core and Secrets.** Test a downstream consumer of defaults, not only the Settings toast. Confirm currency/provider defaults where actually consumed. Save, edit and delete a synthetic secret through its owner; retain only safe IDs in evidence and prove the consumer resolves only the authorised reference. Files override tests must never launch a real user-selected executable; use the existing safe OS/launcher fixture. Provider history Load is explicit; shortening retention requires the existing preview/confirmation and version checks.

**API.** Keep machine credentials distinct from user/admin sessions. Test actual registered-session authority and denial/retry cleanup, not an injected allow-all owner. No plaintext token/password screenshots or trace bodies. Changing business database selection must not transfer the instance-local account/token registry.

**Storage and Resources.** Exercise actual file content and governed re-open. Keep historical versus live source identity distinct. Catalog connection testing retains the shipped draft-driver semantics; the documented filesystem draft refusal must not be 'fixed' into implicit host rebind just for a green UI test. Test current capability/read-only constraints and original profile lifetime.

**Agent and Workflow.** At least one non-live deterministic path uses the real MAF/runtime tool registration, approval, persistence and UI, replacing only the external model boundary. Distinguish that from LIVE-01–03. Test a cancelled or rejected operation with exact no-effect/retained-progress expectations; do not assume cancellation rolls back a committed file. Preserve Projects/Project Structure and Processes HTTP control-plane paths.

**Scheduler.** A passing direct Workflow run does not prove scheduling. Drive the real Scheduler UI and verify an actual scheduled delivery within the isolated runtime with exact fire identity. Use a bounded test clock/fixture only where the real scheduler integration supports it; otherwise a short real one-shot schedule. Do not replay a historical fire or enable unsupported Process scheduling.

**Prompts, Collaboration and TestLab.** Verify selected/created identities survive across the actual consumer boundary. Do not mistake a sandbox notification, pre-seeded escalation or synthetic test-plan run result for a production-created record. Do not require a model tool where the module has no shipped one; inventory and record the actual integration selected for each group.

**Plugins.** Use a packaged inert fixture or a safe existing test plugin. Validate upload, enabled/restart status, exact connection identity, grant admission and a harmless real invocation. No real OAuth login, mail send or external endpoint mutation. Do not weaken plugin approval to make it callable.

**Memory.** Keep shipped capability limitations. A disabled/unsupported ingestion, feedback, push acknowledgement or cancellation path must remain a clear refusal if the current driver does not support it. Do not add these features. Run the actual supported query path and profile-revision provenance regression, plus agent/Workflow memory integration where available. A query result from an old provider revision must not become the current profile's result without clear historical provenance.

**CRM/HR.** Exercise linked person/agent/project/task references and representative sensitive read denial. The live HR creation test adds real proposal/approval coverage; it does not replace the deterministic seven-workspace tests.

## Scheduling the work inside this run

Do not run every heavy suite after every small UI edit. Complete targeted stage proof first. Freeze source and sibling inputs, build, run full Stable, then the broad non-live browser inventory and live lane. Stable and live processes must not share mutable fixture state or environment flags. Diagnostic reruns retain first-attempt evidence; they are not added into a fabricated distinct-pass total.

The user explicitly requested this comprehensive final checkpoint. It is a named broad-gate trigger even when the last code edit is a leaf change. If safe independent suites can run concurrently, isolate output and DB roots and respect repository/test parallelisation policies; don't create competing MSBuild writers.

For each product failure apply TRIAGE.md. Fix small local bugs, rerun all affected groups and re-freeze if a shared change invalidates prior proof. Map complex problems and continue independent safe cases. At the end the report must show what passed, failed, was blocked, was only rehearsed, and what still prevents Workspace or application closure.
