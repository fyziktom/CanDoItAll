# 13. Identities, relationships, and reference shapes

Conceptual shapes define semantics, not a universal new framework. Preserve serialized IDs through compatible adapters. Moving a C# type does not justify changing its ID, route, enum value, or persisted payload.

| Shape | Required meaning | Does not mean |
|---|---|---|
| EntityReference | Owner namespace, kind, owner-issued ID, appropriate scope/origin; historical use identifies revision. | CLR type name, path, or edit grant. |
| StructureNodeReference | Project/scope and existing stable NodeKey; descriptor distinguishes native from projection. | AgentId/RunId or globally unique title. |
| SourceVersion | Opaque owner concurrency revision; ordered cursor only when actually supported. | Lexically ordered GUID or universally authoritative timestamp. |
| OperationIdentity | Registered producer, logical intent/output key, scope/incarnation; separate attempt/correlation. | New GUID on every retry or model-chosen suppression bypass. |
| VerifiedAuthority | Trusted actor/delegation/purpose/source/profile context. | isAdmin=true from UI, HTTP, or tool input. |
| ContributionReceipt | Original fingerprint, effect IDs, committed/effect state, recovery handle. | Promise that the target still exists or provider I/O happened exactly once. |
| RunOriginBinding | Owner/run/step and original destination project/node/role/revision at admission. | Current browser project or another run's LastRunId. |
| ArtifactManifest | Owner run/revision, explicit outputs/roles, content/version/hash, completeness/provenance. | Everything added to a directory/graph since start. |
| AgentCostQuote | Agent/config/model/tariff, workload assumptions, units/currency, range/completeness/validity. | CRM price list, invoice, or budget reservation. |
| ProjectionEnvelope | Source refs/version vector, coverage/cursors, scope/time and availability. | Globally atomic snapshot from independent reads. |

NodeKey can be a prefixed string; no blanket GUID conversion. Entity and occurrence have separate lifecycles. Rename preserves historical reference. Copy creates a native identity with origin under policy; same-space move preserves identity where currently specified.

## Relationship authority

| Relationship | Owner and rule |
|---|---|
| Native containment | Structure; one placement has one parent and no containment cycle. Do not flatten supported multi-placement into a lossy tree. |
| Related-to author link | Structure; kind decides direction/cardinality. It creates no execution/schedule dependency. |
| Project hierarchy | Projects; preserve current multiple-parent semantics rather than force ParentProjectId. Owner checks cycles/edge role. |
| Work dependency | Work Management; explicit scheduling meaning and supported cycle policy. |
| Work assignment | Work Management; task/resource/role/interval and existing set/primary/multiple-performer semantics. Set edits need concurrency. |
| Capacity reservation | CRM/Staffing; assignment/request reference, window, quantity, and policy; not runtime availability. |
| Project participation | CRM; project role/interval, with Projects existence verification. Not duplicate task assignment. |
| Process-step assignment | Processes; execution step/role does not implicitly edit the plan. |
| Definition binding | Structure; owner definition/version and input binding; shared definitions may have several occurrences. |
| Node-to-run history | Structure binding plus runtime-owned runs; multiple runs, selected pointer does not remove history. |
| Asset attachment | Attachment owner; references Storage object/version, with explicit sharing and deletion eligibility. |
| Projected relation | Source owner; Structure displays it and may expose an owner action, not a parallel master edge. |

Each affected kind must map its current enum/type, endpoints, cardinality/cycles, scope, deletion, and tests. Unknown actual details must be discovered, not guessed. Preserve differing legacy semantics via a compatible kind/adapter or an explicit migration.

## Scope and time

Pass explicit source/target scope. Cross-project/profile reference requires supported policy; equal IDs in different histories are not the same identity. Data profile is not automatically tenant and workspace is not automatically project.

View coordinates/row order differ from work dates. Preserve instant versus local date, null dates, calendars, recurrence/timezone, and time units. Scheduler owns cron/timezone/misfire, not the Gantt renderer. No incidental scheduling redesign.

## Reentrance and retention

Bidirectional published APIs do not authorize synchronous callback loops. CRM may request a task assignment that needs CRM reservation; a coordinator calls distinct lower-level owner operations in defined order, not recursively the original top-level request. Invalidation cannot repeat mutation. Projectors cannot relaunch the run whose results they read. Events must not endlessly republish the same change without origin/causation suppression.

Contract/adapter assembly graphs are acyclic and runtime interactions have bounded responsibilities. Validate lock order, hop/iteration budgets, and effect deduplication; these controls do not grant authority.

Receipt/dedupe retention covers permitted retry/replay and queued delivery. After deliberate history expiry, an old key is expired/unknown, not automatically new. Restore/incarnation changes invalidate caches/cursors but must not regenerate remote keys and repeat completed external work. Retain external provenance and reconcile pending operations. No exactly-once guarantee is made for non-idempotent providers.
