# SB00 provider usage and deletion characterization

Captured: 2026-08-24  
Evidence mode: static source characterization  
Product behavior changed by this artifact: **No**

## Deletion behavior

There is no current general provider reference/deletion service.

| Path | Source anchor | Current effect |
| --- | --- | --- |
| Workspace delete | `WorkspaceService.DeleteProviderAsync`, `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:510` | Hard-deletes the EF row, notifies observers, records activity; performs no reference check or dependent update. |
| AgentFramework registry delete | `WorkspaceBackedAgentProviderProfileRegistry.DeleteProviderAsync`, `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs:218` | Hard-deletes the same EF row, removes the runtime snapshot through observers, then removes the sandbox shadow provider. |
| Catalog facade cleanup | `AgentFrameworkWorkspaceCatalogService.DeleteProviderAsync`, `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.ProvidersAndCapabilities.cs:31` | After registry deletion, runs a second catalog mutation that sets matching `AgentDefinition.ProviderProfileId` values to `null`. |
| Current-profile projection | `CurrentProfileAgentFrameworkWorkspaceService.DeleteProviderAsync`, `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs:309` | Synchronizes the directory projection only after the inner delete returns. |

The separate post-delete operations are not atomic. A failure after the EF commit can leave an
agent or directory projection referring to a deleted provider. The Workspace-only delete does
not attempt these cleanup steps at all.

Known retained references include:

- `WorkspaceSettings.DefaultProviderProfileId`,
  `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:23`;
- current/historical provider IDs embedded in AgentFramework catalogs and execution evidence;
- Simple Chat definition revisions and invocation evidence;
- workflow/runtime records and other module models that store provider IDs without an EF FK to
  `Workspace_ProviderProfiles`.

Historical invocation evidence should remain immutable. Active configuration references require
an explicit reference query and removal/migration policy. The shared-provider implementation must
not infer that `DeleteProviderAsync` currently supplies such a policy.

## Agent usage observation and persistence

`ProviderUsageObservation` is defined in
`src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderUsageModels.cs:23`.
It records provider/model/transport/source phase, completeness, token counts, request/response
identifiers, run/agent/session correlation, pricing evidence, and diagnostic JSON.

Agent execution inserts or replaces observations by observation ID in
`AgentFrameworkWorkspaceExecutionService.InsertUsageObservations`,
`src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:1105`.
The file-backed execution store persists them under each run's `usage` directory or the orphan
usage directory and reloads them through `LoadProviderUsageEvidenceAsync`:
`src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs:55`.

`AgentProviderUsageProjectionSource` at
`src/MAF/Common/CanDoItAll.AgentFramework.Core/Usage/AgentProviderUsageProjectionSource.cs:7`
maps that durable evidence to the common provider-usage projection. It preserves missing and
unavailable usage instead of converting them to known zero usage.

## Simple Chat usage observation and persistence

`LlmChatInvocationRecordRow` in
`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Entities/LlmChatPersistenceRows.cs:236`
stores a provider snapshot, model, usage/pricing status, tokens, cost, outcome, timestamps, and
correlation ID. `LlmChatInvocationRecordConfiguration` uses `(OperationId, Ordinal)` as the key
and indexes `(ProviderProfileId, StartedAtUtc)`:
`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/EntityConfigurations/LlmChatOperationConfigurations.cs:41`.

`EfLlmChatUnitOfWork.SaveChangesAsync` rejects updates or deletes of invocation records, making
them append-only:
`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Repositories/EfLlmChatUnitOfWork.cs:87`.

`SimpleChatProviderUsageProjectionSource` at
`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Usage/SimpleChatProviderUsageProjectionSource.cs:11`
joins invocation, operation, conversation, and immutable definition revision rows and maps them
to the common usage projection.

## Common usage extension point

The common contracts are in
`src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageContracts.cs`:

- `ProviderUsageContribution` at `:80`;
- `IProviderUsageProjectionSource` at `:125`;
- usage, pricing, outcome, source-state, workload, and consumer enums at `:5-56`.

`ProviderUsageQueryService` at
`src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs:3`:

- reads selected projection sources concurrently;
- excludes failed-source contributions but reports source status;
- deduplicates by `(WorkloadKind, ContributionId)`;
- rejects conflicting duplicate contributions and conflicting terminal outcomes;
- counts only `Observed` and `LegacyKnownTokens` as known usage;
- never adds tokens or cost for missing/unavailable observations.

Agent and Simple Chat sources are registered additively through
`TryAddEnumerable<IProviderUsageProjectionSource>` in
`src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs:205`
and
`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/LlmChatsPersistenceServiceCollectionExtensions.cs:37`.

## Truthful shared-relay projection decision

The relay should persist one metadata-only `SharedProviderInvocationRecord` and expose one
additional `IProviderUsageProjectionSource`. The invocation ID is the stable contribution ID so
idempotent finalization/retry cannot double count.

The current workload/consumer enums know only Agent, Simple Chat, and Unknown/Unattributed.
`ProviderUsageWorkloadSelectionExtensions.Includes` at
`ProviderUsageContracts.cs:217` includes `Unknown` only when the caller selects `Both`.
Classifying external relay traffic as Agent or Simple Chat would be false; classifying it as
Unknown would make filtering and UI semantics accidental. Add a dedicated
`SharedProviderRelay` workload and consumer through the existing usage project and update
selection/UI semantics.

Required relay mapping:

- provider-reported usage -> `Observed` plus `ProviderReported` pricing when present;
- locally calculated price -> `CalculatedAtExecution`;
- upstream activity with absent usage -> `MissingAfterProviderActivity` or
  `UsageUnavailable`, never zero tokens;
- failure/cancellation -> explicit terminal outcome with any actually observed terminal usage;
- no request/response content, tool arguments/results, attachment bytes, secret IDs/values,
  private endpoint, or raw upstream error body in the invocation record.

## Minimal characterization gap

Existing tests already cover projection failure, runtime snapshot invalidation, secret dispatch
scope, usage normalization, Agent usage projection, and aggregation. One focused deletion matrix
test remains valuable: create a provider referenced by Workspace default, an agent, and a Simple
Chat definition; invoke the real AgentFramework delete path; record which references are cleared
and which remain. This test characterizes the current gap and must be replaced by policy tests
when provider publication/import deletion behavior is implemented.

