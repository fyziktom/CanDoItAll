# Repository evidence

## Baseline

- branch: `development`
- commit: `c0117109c6ef6166d1d8b1b42d75e7f4af83c5ee`
- merge: `unix-adoption`
- current host contract: Windows, Linux, and macOS
- `.NET`: 10
- primary database: PostgreSQL 16-compatible server
- canonical solution: `CanDoItAll.slnx`

## Existing lightweight LLM layers

### `CanDoItAll.AgentFramework.Llm.Abstractions`

Already owns:

- `LlmMessage` and roles;
- bounded binary `LlmAttachment`;
- `LlmResponseFormat`;
- `LlmModelSettings`;
- `LlmInvocationRequest`, result, usage, and typed failures;
- `ILlmInvocationPort`;
- ordinary conversation contracts.

`LlmModelSettings` currently carries a provider-neutral JSON parameter envelope whose documented
examples include reasoning effort, but it does not expose thinking effort as a first-class typed
property. The provider/model capability truth already exists in `AgentFramework.Models` through
`ProviderModelThinkingEffortCapability`, `AgentReasoningEffortLevel`, and
`AgentThinkingEffortPolicy`; provider profiles can carry different capabilities per model. SB00 must
lock reuse/ownership and SB01/SB04 must add the smallest typed lightweight-path seam without copying
that catalog into LLM Chats.

### `CanDoItAll.AgentFramework.Llm.ProviderRuntime`

Already owns the provider-backed stateless invocation adapter. It dispatches through the provider
runtime pool and does not construct an agent, tool graph, session, workspace, or process. At the
prepared baseline, the `ILlmInvocationPort` DI registration is hosted by the Workflows runtime
extension; SB00 must verify and, if unchanged, move the reusable registration seam into the
provider-runtime owner so LLM Chats does not depend on workflow activation.

### `CanDoItAll.AgentFramework.Llm.Conversations`

Already owns:

- canonical application transcript;
- provider/model snapshot and explicit switch policy;
- optimistic transcript revision;
- atomic pending-user admission;
- active-turn marker and compensation;
- bounded non-destructive context-window selection;
- file-backed and in-memory stores;
- explicit active-turn abandonment.

The file-backed store is suitable for isolated/library use, not the production API store. It has
process-wide, not cross-process, coordination and lists documents by enumerating JSON files.

## Intentional production dormancy

The ordinary conversation library is currently not registered in the product composition root and has
no current HTTP API or UI. A previous production registration was removed because a scoped service
captured a potentially stale storage root across database-profile switching.

This bundle must not restore that registration. It creates a product-owned engine over PostgreSQL and
the existing invocation port.

## Database architecture

- `AppDbContext` applies module `IEntityTypeConfiguration<T>` implementations from assemblies
  registered through `AppDbContextModelRegistry`.
- application-managed concurrency tokens use `IHasConcurrencyToken`.
- PostgreSQL migrations belong to
  `src/Foundation/CanDoItAll.Migrations.PostgreSql`.
- the initial baseline migration is immutable; new migrations append after it.
- focused migration proof uses `MigrationBootstrapIntegrationTests` and
  `dotnet ef migrations has-pending-model-changes`.

## Runtime profile identity

`IDatabaseRuntimeState` exposes:

- active profile ID;
- active fingerprint;
- monotonic generation.

`IDatabaseSwitchNotificationService` publishes profile changes. This is the sole canonical identity
source for LLM Chat operation fencing. The prepared baseline also exposes the canonical provider
profile snapshot through an interface located in AgentFramework Core even though the implementation is
provider-runtime infrastructure; SB00 must resolve that ownership mismatch instead of making the new
module reference AgentFramework Core.

## HTTP and testing

- product HTTP adapters live under `src/App/CanDoItAll.Web/Api`;
- non-trivial behavior belongs in product modules;
- real-host API integration tests use a focused fully-qualified-name filter;
- the stable Release gate is solution-wide and expensive;
- the current CI matrix proves Windows, Ubuntu, and macOS host behavior.

## Known UI coupling, intentionally outside this bundle

Agent floating chat, `AgentChatPanel`, `ChatWorkspacePanel`, approvals, execution logs, voice, and
runtime details remain agent-specific. They must not be modified here.
