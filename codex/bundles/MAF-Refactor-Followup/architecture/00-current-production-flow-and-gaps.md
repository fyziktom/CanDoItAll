# Current production flow and gaps

## Floating send path

```text
AgentChatPanel
  -> AgentChatExecutionOrchestrator
  -> AgentTurnContextCaptureService
       -> route-fenced AgentChatContextSnapshot
       -> CanonicalAgentExecutionAuthorityResolver
       -> AgentTurnContextReference + authority projection in metadata
       -> transient context with authority WorkspaceScope
  -> AgentFrameworkWorkspaceExecutionService
       -> ExecutionRunRecord
       -> BuildRuntimeExecutionOptions
       -> CreateRuntimeContextIntent (legacy/process metadata)
  -> IAgentExecutionRuntime
  -> MafAgentExecutionAdapter
       -> WorkspaceRuntimeServicesFactory.Create(effective scope)
       -> MafRuntimeAgentFactory / RuntimeCapabilityComposer
       -> tools + provider call
```

## Gap

The full `AgentExecutionAuthorityRecord` does not cross the `AgentFrameworkWorkspaceExecutionService` boundary. Only a safe projection is stored, and the runtime reconstructs intent from legacy execution metadata. Scope is indirectly carried by transient context, while read/mutation/allowed-operation facts are not authoritative inputs.

## Required target

```text
Turn admission
  -> immutable AgentExecutionGovernanceSnapshot
       identity + profile/generation + scope
       read/mutation + allowed operations/capabilities/aliases
       policy version/fingerprint
  -> execution command + durable safe projection + in-memory continuation lease
  -> capability planner intersects snapshot with agent config/module restrictions
  -> invocation policy enforces the same snapshot
  -> approval decisions authorize specific proposals only
```

The snapshot must be immutable for the full turn and approval continuation. A new UI state creates a new turn snapshot; it never mutates the old run.
