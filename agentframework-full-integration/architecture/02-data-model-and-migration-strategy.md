# 02 — Data Model And Migration Strategy

## New Persistence Additions

### Collaboration module

Recommended entities:

- `NotificationInboxItem`
- `NotificationInboxRecipient`
- `ConversationThread`
- `ConversationParticipant`
- `ConversationMessage`
- `EscalationRecord`
- `CollaborationContextLink`

Key fields to preserve:
- `ProjectId`
- `ProcessDefinitionId`
- `ProcessRunId`
- `ProcessStepRunId`
- `LaunchPlanId`
- `CorrelationId`
- `CausationId`
- `CreatedAtUtc`, `UpdatedAtUtc`, `ReadAtUtc`

### Processes module additions

Recommended entities:

- `ProcessRoleMessagingRule`
- `ProcessLaunchPlan`
- `ProcessLaunchPlanRole`
- `ProcessLaunchCandidate`
- `ProcessLaunchApprovalRecord`
- `ProcessLaunchProvisioningRequest`
- optional `ProcessRunMessagingPolicySnapshot` if not embedded into run snapshot JSON

### CRM-HR additions

Recommended entity:

- `AiResourceBinding`
  - links `PartyId` to `AgentDefinitionId`
  - stores binding metadata and sync status

Potential projection support:
- extend `StaffingRequest` with optional source metadata or add a separate link table so process launch demand can be visible in CRM-HR without making CRM-HR the owner of the launch lifecycle.

### AgentFramework module additions

Recommended persistent shapes inside integrated store:

- `AgentDefinitionRecord`
- `AgentTemplateRecord`
- `AgentExecutionRunRecord`
- `AgentExecutionApprovalRecord`
- `AgentExecutionCheckpointRecord`
- `AgentChatSessionRecord`
- `AgentChatMessageRecord`
- `AgentArtifactProjectionRecord`

Bundle intentionally nevyžaduje, aby všechno bylo v jednom EF souboru nebo jednom god service. Naopak očekává modulárně rozdělené stores / services.

## Migration Order

1. **Foundation migrations**
   - Add new `Collaboration` tables.
   - Add `ProcessRoleMessagingRule`.
   - Add `ProcessLaunch*` tables.
   - Add `AiResourceBinding`.
   - Add initial `AgentFramework` integrated tables.
2. **Backfill migrations**
   - Convert existing CRM-HR AI agent technical fields into initial AgentFramework definitions + bindings.
   - Project existing provider profiles into integrated provider bridge compatibility views if needed.
3. **Feature-flag transition**
   - Turn on new write paths.
   - Freeze legacy direct writes.
   - Keep legacy reads only where needed for rollout safety.
4. **Cleanup migrations**
   - Remove or deprecate obsolete duplicate fields and indexes after proof passes.

## Backfill Rules

### Provider profiles

- Do not duplicate `Workspace_ProviderProfiles`.
- Backfill only compatibility projections if AgentFramework integrated stores need cached runtime metadata.
- Secrets stay in existing secret storage; no rehydration to environment-variable-only ownership.

### CRM-HR AI agent profiles

Current fields like `ProviderProfileId`, `DefaultModel`, `ExecutionMode` are technical/runtime flavored. Target migration:

1. Read current `AiAgentProfile`.
2. Create or map corresponding `AgentDefinitionRecord`.
3. Create `AiResourceBinding`.
4. Mark CRM-HR technical legacy fields as compatibility-only until cleanup.
5. Switch CRM-HR save flow to the new facade.

### Existing process runs

- Existing historical runs do not need retroactive messaging threads if none existed.
- Launch plans are required only for runs started after the migration feature flag is enabled.
- Historical process artifacts remain valid; artifact bridge applies forward.

## Data Integrity Rules

- No launch plan may start an actual `ProcessRun` until all required roles are either assigned or explicitly approved as unresolved exception.
- No direct conversation thread may exist for a process run unless a matching process messaging rule or escalation policy exists.
- No AI resource binding may point to a missing agent definition.
- No provider runtime call in integrated mode may resolve credentials from ad hoc environment variables when a Workspace secret exists.
- No process artifact may point only to a workspace-relative path without a canonical managed storage mapping, unless it is explicitly marked transient and excluded from evidence.

## Migration Validation Requirements

- SQL/EF integration tests for backfill.
- Regression tests for existing CRM-HR agent profiles and provider profiles.
- Proof that legacy Settings provider UI no longer creates a second runtime path.
- Proof that old AI agent pages still open and show migrated data after backfill.
- Proof that new runs create launch plans, messaging rules and bindings correctly.

## Suggested EF Migration Packaging

- Keep migrations grouped by theme rather than giant omnibus migration:
  - `AddCollaborationFoundation`
  - `AddProcessLaunchAndMessagingPolicy`
  - `AddAgentFrameworkIntegratedPersistence`
  - `BackfillAgentDefinitionsAndBindings`
  - `RetireLegacyProviderExecutionPath`
  - `CleanupLegacyAiAgentRuntimeFields`

This keeps rollback and review understandable.
