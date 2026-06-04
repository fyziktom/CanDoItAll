# Target Refactoring Architecture

## Layering Goal

Move from "large partial classes carrying many implicit contracts" toward a layered architecture:

```text
Templates / Skills / Agent Instructions
        |
Canonical Contract Catalogs
        |
Runtime Policy Services
        |
Execution / Dispatch / Workflow Runtime
        |
Evidence / Usage / Artifact Ledgers
        |
API DTOs and UI Display Adapters
        |
Tests / E2E Process Runs / QA Verifier
```

## Proposed Contract Catalog Areas

### ProcessContractCatalog

Owns:

- process operation ids
- operation target scope ids
- step execution boundary values
- artifact expectation status values
- branch outcome semantics
- recovery action ids
- process run status display mappings
- process step status display mappings

### ToolContractCatalog

Owns:

- workspace tool ids
- browser tool ids
- runtime command tool ids
- command lifetime semantics
- keepAlive semantics
- cleanup receipt shape
- denied-tool diagnostic codes

### EvidenceContractCatalog

Owns:

- managed artifact path patterns
- current-run binding requirements
- browser proof artifact schema
- runtime command proof schema
- screenshot proof schema
- source assertion schema
- proof validation statuses

### WorkflowExecutorContractCatalog

Owns:

- executor ids
- executor availability state
- side-effect classification
- dry-run support
- idempotency key requirements
- processed-marker policy
- retry semantics

### ProviderUsageContractCatalog

Owns:

- usage observation schema
- usage source phases
- usage statuses
- pricing profile version/hash
- provider kind/model ids
- reconciliation statuses

## Refactoring Seams

### Process Dispatch

Candidate extracted services:

- `IProcessStepOperationPolicyEvaluator`
- `IProcessArtifactExpectationEvaluator`
- `IProcessArtifactProjectionService`
- `IProcessStepCompletionFinalizer`
- `IProcessRunLineageValidator`
- `IProcessRuntimeProofValidator`
- `IProcessDispatchConcurrencyGate`
- `IProcessRunCostSynchronizer`

### Agent Runtime

Candidate extracted services:

- `IProviderUsageRecorder`
- `IAgentRuntimeUsageCollector`
- `IAgentFinalizerUsageBridge`
- `IStructuredOutputRepairUsageBridge`
- `IAgentRunFailureUsageBridge`
- `IAgentToolTraceCollector`

### Workflow Runtime

Candidate extracted services:

- `IWorkflowExecutorAvailabilityPolicy`
- `IWorkflowSideEffectPlanner`
- `IWorkflowIdempotencyService`
- `IWorkflowProcessedMarkerPolicy`
- `IWorkflowDryRunExecutorAdapter`

### UI

Candidate extracted services/models:

- typed workflow canvas view models
- typed provider/capability setup descriptors
- process status display adapter
- process proof display adapter
- token/cost display adapter
- executor availability display adapter

## Compatibility Strategy

- Keep public API DTOs compatible unless a versioned DTO is introduced.
- Add adapters around numeric enum HTTP shape rather than changing wire contracts abruptly.
- Introduce new canonical descriptors first, then migrate callers incrementally.
- Keep old metrics readable while introducing the new usage ledger.
- Keep existing process templates importable and add compatibility tests before template normalization.

## Anti-Goals

- Do not rewrite the whole process engine.
- Do not replace the working Tetris flow with hard-coded app-generation logic.
- Do not merge workflow and process runtime cost scopes unless correlation is explicit.
- Do not accept a UI-only display fix for a runtime contract drift.
- Do not add broad fallback behavior that hides provider/tool/executor failures.
