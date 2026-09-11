# CanDoItAll.Modules.AgentFramework

## Purpose

Product module that exposes AgentFramework catalog, provider, governed execution, agent chat, Simple
Chats presentation, shared provider-usage analytics, and technical-agent bridge capabilities to the app
runtime.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.AgentFramework.csproj](CanDoItAll.Modules.AgentFramework.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

The module adapts the generic activity/preparation contracts to the current database
profile:

- `AgentChatExecutionOrchestrator` admits an activity operation and returns its stream
  handle before awaiting context capture or execution.
- `CurrentProfileAgentExecutionActivityReader` authorizes database profile, generation,
  and organization workspace scope and cancels readers when that profile lifetime
  changes.
- `AgentChatPreparationPool` is circuit-scoped metadata preparation for active agent
  definitions only.
- `AgentExecutionPreparationCache` is scoped immutable execution preparation.
- `CanonicalProviderRuntimeProfileSnapshotService` is a singleton immutable provider
  projection fenced by database profile identity/generation and persistent provider
  concurrency revisions.

Provider database rows remain canonical. Save/delete commit observers update the
runtime projection after commit; a projection failure faults the snapshot explicitly
without hiding or reversing the canonical commit. Use-time revision probes either
confirm the immutable lease, refresh the changed provider, or fail closed.

Resolved secret values are not stored in the provider snapshot or preparation cache.
They are prepared for one execution dispatch, checked against the provider
configuration fingerprint, and cleared on scope disposal. Live MAF runtimes remain
per execution.

Execution source authority is composed from the registered
`IAgentExecutionSourceAuthorityProvider` implementations. Product-specific providers belong to their
owning Projects, Workbench, and Processes modules; this module supplies the registry and canonical
resolver, not hard-coded knowledge of those products. Persisted authority restoration and approval
continuation fail closed on malformed or mismatched authority, and on missing authority when the run
proves governed context admission. Detached or legacy runs without that evidence remain explicitly
ungoverned. Tool-policy evaluation returns the exact effective invocation context, and that same context
is used by the runtime tool provider.

Runtime-owned child-process leases are cleaned only through an effective workspace/profile scope. The
cleanup boundary re-reads durable terminal execution state and does not release leases for running or
waiting-on-tool executions.

The separate `CanDoItAll.AgentFramework.Llm.Conversations` library remains an opt-in ordinary LLM
conversation foundation and is not globally registered. The Simple Chats product composes it behind
profile-generation fencing, PostgreSQL persistence, retention, leases, and durable operations. This
module hosts the Simple Chats workspace, floating-shell contribution, Prompt Gallery composer action,
and usage projection; it does not route those conversations through agent execution. Web owns the
separate authorized HTTP/OpenAPI adapter.

## Project-access deletion persistence

AgentProjectAccessDbContext maps only the existing project-access revocation record.
The complete model remains the sole schema and migration authority. Runtime factories
bind to the immutable canonical database profile. Preparation opens an explicit
enlisted owner context, saves the durable revocation in Projects' transaction, and
returns only the project/recovery identity. Postcommit recovery, status updates, and
history use ordinary independent owner contexts.

Recovery is keyed by project lifetime. Existing recovery IDs, status values and claim
fields are retained. Legacy records keep null profile/lifetime metadata and their
filtered unique ProjectId index; new records have a unique (ProjectId, ProjectLifetimeId)
index and paired profile/lifetime metadata. The canonical migration owns these additions. AttemptCount is also the durable claim generation: eligible claims compare
and increment the observed value, and renewal/completion/failure require that exact
generation, Processing status, and a live lease. Each transition locks its exact row
in a short owner transaction, then its conditional UPDATE samples PostgreSQL
clock_timestamp() once after the lock wait. UpdatedAtUtc renews ownership while LastAttemptAtUtc preserves attempt start. Fresh
Processing recoveries expose retry availability instead of apparent completion.
Failure persistence is bounded; ownership loss cancels workspace processing and cannot
rewrite a successor's recovery state.

Revocation matches the captured profile/project/lifetime inside the existing
cross-process catalog mutation. AllowedProjectIds remains the compatible projection;
allowedProjectLifetimes records exact bindings and normal metadata writes retain them
for selected IDs. Old cleanup cannot remove another bound lifetime. Legacy unbound
grants retain their explicit cleanup behavior. These guarantees require the grant
writer cutover before all bare-ID authority is fenced: durable creation reservations,
bootstrap binding, editor/import admission, runtime access enforcement and explicit
profile transfer/purge remain required dependent work. No external exactly-once claim
is made. All participating workers must use generation-aware finalization;
older binaries still use the legacy unguarded updates.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Agent execution activity and runtime snapshots: `docs/architecture/internal-communication.md`
- Reusable floating agent chats: `docs/architecture/internal-communication.md`
- Simple Chats product and API: `docs/llm-chats-api.md`
- Simple Chats integration ownership: `docs/architecture/llm-chats-boundary-and-handoffs.md`


Workflow persistence uses the explicit seventeen-record `WorkflowDbContext`: definition
versions and heads, components/settings, runs/events/artifacts/checkpoints, request and
response recovery, launch/executor claims, usage facts, backend checkpoint sessions and
payloads, and retained Structure output manifests. The canonical pooled factory binds
to the immutable database profile. The historical floating-chat settings key remains in
the shared Workflow settings table and uses this same owner factory.

The runtime model excludes Prompt Gallery entities while retaining the component
reference columns and both lookup indexes. The complete canonical migration model
retains both physical Prompt foreign keys. Definition-head `VersionId` concurrency is
separate from automatic GUID stamps on request boundaries and response operations;
request/operation versions and lease epochs retain their existing explicit protocols.
History projection, usage append and resume commit keep explicit owner/History
transaction enlistment. Ordinary factories remain independent. This cutover does not
move schema/migration authority, change saved payloads, or complete target-profile
transfer, Agent history locators, producer authority or project lifetime admission.
