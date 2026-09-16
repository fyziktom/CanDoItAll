# 8. Execution and preservation of product journeys

These are target behavior contracts. USER denotes user-confirmed paths; CODE/DOC have catalog sources. New recovery guarantees are target requirements, not claims that all current backends satisfy them. A poorly located adapter is not permission to remove the positive capability.

## JRN-01 — Floating agent over Structure/Gantt

The user opens a project and selects nodes/tasks. A domain context adapter captures an immutable bounded snapshot with scope/profile/generation/fingerprint, selection, purpose, and coverage. Shell owns window/focus/subscriptions only. Agent adapter evaluates capabilities and starts execution; no component, EF object, DI scope, or self-authorizing path enters the snapshot.

Reads choose invocation snapshot or canonical current explicitly. Writes invoke the destination owner with current authorization and revisions. Receipts return real IDs; canvas/Gantt invalidate after commit and display the same task. Moving the window between views does not replace history or start a second execution. A new turn may capture new context; admitted work retains its origin. Switching project/profile cannot retarget a pending write or result.

Closing a surface is not an automatic cancel command. Characterize any legacy session-bound policy explicitly rather than silently changing it. Simple Chats documents durable operation continuation independent of the surface [SRC-006]. Catch visual-only regressions where the window works but tools disappear, stale context crosses profiles, or the model says done after owner rejection.

## JRN-02 — Agent files, notes, and tasks

Native note is inline Structure content. Text/JSON/Markdown/Mermaid/log asset uses real Storage bytes plus a binding; Notes is not fake file content. Resource promotion is a separate explicit operation. FileTools receives authorized Project/Resource/Run scope and validates containment, root, MIME, and limits at use. A secret-reference node does not authorize serializing a secret into content.

Tasks go through Work Management so task-kind, attachment, assignment, and scheduling invariants apply. Retain agent -> typed adapter -> owner authoring; prohibit foreign writes, not collaboration.

## JRN-03 — Workflow launch and return

Structure owns a definition/version reference or explicit selection policy. Resolve valid owner definition/backend and bounded authorized inputs, then create a stable start intent. Workflow owner admits RunId plus immutable origin project/node/binding revision/output role/parentage/delegation. UI observes that receipt.

Workflow Structure executor retains supported typed operations [SRC-027]. Completion publishes an owner manifest; destination adapter attaches exact output to the intended run/reference. RunAccepted, ExecutionSucceeded, OutputAttached, and TaskAccepted are separate. Projection failure cannot restart a paid run. Older late completion must not replace newer selected status merely because LastRunId is mutable; all histories retain lineage.

GetStatus currently applies projection status [SRC-026]. Moving it to a clean query requires an independent refresh/projector so output delivery works without an open page. Manifest provenance, not newly observed node-set difference, attributes output. Invalid backend/version/input is rejected before admission; no silent fallback.

## JRN-04 — Processes and subprocesses

Preserve definition discovery, variable preparation, readiness, governed launch, agent/workflow executors, observation, history, and writeback. Process owner records execution state; Structure only projects it. Child receives its own run identity and parent/causation with no broader delegation/output scope. Runtime executor assignment does not overwrite Work Management's plan.

Recovery preserves completed effects/checkpoints and reconciles uncertain effects before repeating. Technical process success does not automatically satisfy task acceptance. Keep supported HTTP and Structure bridge paths; the audit does not establish a general Process tool provider [SRC-028].

## JRN-05 — Planning, CRM resources, and cost

CRM and canvas modify the same WorkAssignment through Work Management. CRM retains project participation and staffing facts. Mandatory capacity reservation is coordinated with the assignment under its declared atomic/pending policy; a capacity block is not automatically a reservation.

Agent/provider prices come from the technical owner, with workload, version, units, currency, validity, and uncertainty. Store the chosen quote as a historical baseline, not a CRM price master. Runtime admission still independently checks current provider/permissions/limits. Unknown/partial cost cannot become zero, and parent/child views must not duplicate invocation evidence.

## JRN-06 — Scheduler dispatch

Plan owner records target/version, time semantics, input, and firing identity. Dispatch retries the same firing with a stable start key; target runtime owns the actual run. Preserve timezone, DST, misfire, enable/disable, and recovery rules. Trigger-engine state is not canonical plan data.

Verify support per surface. Current managed Scheduler provider is interactive and workflow-only [SRC-031]; a general module description does not prove process tool support. Unsupported targets are explicit, not fake launches.

## JRN-07 — Agent/provider edits and CRM projections

Agent saves go to Agents, provider saves to Providers. Commit differs from invalidation and CRM projection delivery. CRM-owned synchronization retains local staffing facts. Do not independently “fix” technical fields in CRM.

Dispatch validates frozen configuration/revision, resolves secrets for the actual use, and records pricing provenance. Catalog changes may invalidate a new quote but do not reprice historical invocation evidence. Fallback requires explicit capability/cost/policy authorization, never random healthy-provider selection.

## JRN-08 — Deferred asset generation

Preserve placeholder -> running -> actual media or failed behavior. The inspected queue is in-memory [SRC-024], not automatically restart-safe because metadata has OperationId. A changed boundary either implements durable admission/claim/recovery or explicitly retains the scoped ephemeral limitation. Unknown provider completion requires lookup/reconciliation; no invented exactly-once billing guarantee.

## JRN-09 — Delete and late output

Explicit delete disposition distinguishes graph/binding removal from eligible owned-content deletion. Hiding projected history does not delete runtime history. Partial multi-root outcomes remain partial [SRC-005]. Confirmation never bypasses provenance.

After target deletion, runtime keeps manifest/result and destination reports blocked/deleted/suppressed. No new current-parent target or resurrection. Authorized reattachment is a new intent retaining origin. A Deleting project rejects new writes that would refill cleanup.

## JRN-10 — Copy/move/export/import

Copy creates new native IDs under explicit policy, not new foreign source masters. Move preserves IDs where the existing contract says so; cross-scope remapping is explicit. Define remapping/retention for links, assignments, content, and receipts. Running origin is frozen, never implicitly retargeted by moving a node.

Each import participant validates its owned schema/references. Do not activate imported runs or secrets. Export is not serialization of a global context/cache. Preserve native notes and historical evidence; projections may be rebuilt with source provenance.

## JRN-11 — Memory, Prompts, TestLab

Runs pin prompt versions. Curator changes use Prompts owner. CRM/Resource/Structure snapshots enter Memory through authorized source gateways; retrieval does not prove current source existence/access. TestLab verdict identifies exact target/content revision; an agent saying tests passed is not TestEvidence.

Reattach actual used capabilities through product-owned adapters when cutting large references. Do not lose these integrations as peripheral features.

## JRN-12 — Headless and presentation hosts

Owner operations work without Razor pages. Desktop-only terminal/Explorer/elevation are explicit capabilities. A fake presentation host proves rendering/state, while actual PostgreSQL/storage integration proves transactions/adapters. Null services making a host start are not proof: required security/writer/execution dependencies fail closed or invalidate configuration. Do not offer nonexistent actions.

## JRN-13 to JRN-18 — Cross-module automation

The complete continuations are in [17](17-operation-protocol-and-multi-owner-journeys.md): HR agent administration, CRM contact creation, Simple Chats definition management, CRM/Resources reads for a task, workflow/process writes to foreign owners, and Scheduler/curator operations. These use the same owner rules without funneling through Structure. Their baseline/extension distinction is recorded in [runtime surfaces](../catalogs/runtime-surfaces.md).
