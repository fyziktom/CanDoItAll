# Target Architecture

## 1. Process Operation Contract Layer

Introduce a single `IProcessStepOperationContractResolver` used by:

- process template import/publish linting,
- run start validation,
- process dispatch,
- tool invocation policy context creation,
- UI blocker display, and
- proof validators.

The resolver must return an explicit result:

```text
Resolved | Missing | Invalid | LegacyImplicit | MigrationRequired
```

`GovernedLive` must allow only `Resolved`. `LegacyImplicit` may be available for old draft definitions or migration previews, but not for production automation.

## 2. Tool Capability Registry

Replace the split catalog/metadata/classification behavior with one canonical registry:

```text
ToolId
DisplayName
ProviderKind / HostKind
Classification: Read | Mutation | Validation | RuntimeLaunch | BrowserEvidence | BrowserInteraction | ExternalAction | LocalMcp | ProviderNative
OperationRequirements[]
TargetScopeRequirements[]
RequiresApprovalByDefault
CanMutateProduct
CanExecuteExternalAction
CanReadExternalTarget
CanWriteManagedArtifact
BrowserProofRole
SideEffectDescriptor
IdempotencyDescriptor
```

No fallback-to-read is allowed for unknown tool names. Unknown tool names are either provider-native/MCP with their own controlled metadata path, or denied with a clear reason.

## 3. Provider Usage Ledger

Provider usage observations are canonical. Legacy `AgentRunMetric` exists only for backward compatibility and display summaries.

Required normalized fields:

- provider name/kind/transport,
- model,
- provider response id,
- provider request id when available,
- source phase,
- usage status,
- input tokens,
- cached input tokens,
- output tokens,
- reasoning tokens,
- total tokens from provider when available,
- raw usage JSON,
- raw response status,
- calculated cost,
- provider-native cost if available,
- execution/process/workflow correlation.

## 4. Real Process E2E Harness

The proof harness must do only this:

1. seed project structure with request packet,
2. start a process run in `GovernedLive`,
3. let automation dispatch run,
4. approve required tool calls when configured,
5. wait for terminal state,
6. collect execution runs, tool receipts, artifacts, usage, and browser proof,
7. validate generated app behavior.

It must **not** write production app code itself when claiming app-generation proof.

## 5. Proof Quality Gate

A final proof-quality checker must classify proof artifacts by kind:

- production path proof,
- migration/backfill fixture,
- browser-only regression,
- manual process API fixture,
- synthetic unit test,
- red-team adversarial proof.

A critical subbundle cannot close with only fixture/browser/process-API proof when the requirement is production automation behavior.
