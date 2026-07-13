# B07 — Regression harness

## Goal

Create reliable tests that reproduce the failure class without requiring a live LLM.

## Test categories

### 1. Runtime state machine tests

- parent subprocess step with no child → launches child and defers parent;
- parent subprocess step with active child → remains waiting/deferred;
- parent subprocess step with accepted child handoff → parent completed and produced slot available;
- parent subprocess step with child no-go packet → parent blocked with no-go diagnostics;
- parent subprocess step with child completed but no accepted output → parent blocked with concrete missing child handoff diagnostic.

### 2. Adapter tests

- adapter does not call `ExecuteRunAsync` for runtime-owned subprocess when coordinator can handle it;
- adapter includes bridge-generated receipt and managed artifact ref;
- adapter maps no-go child result to `NeedsManager` with child evidence.

### 3. Projection/operator tests

- exact execution observation is found even with many execution runs in one process run;
- missing AF observation uses runtime receipt diagnostics;
- operator hint names expected produced artifact title/key, not only slot count.

### 4. Capability preflight tests

- required tool not composed blocks before agent run;
- denied project-structure scoped access gives `required_tool_denied`;
- available tool allows dispatch.

### 5. Template validation tests

- subprocess contract accepted/no-go outputs compile;
- repair handoff is accepted for `prepare-solution-skeleton`;
- manual skip policy is explicit.

## Suggested test names

```text
PrepareSolutionSkeleton_WhenChildCompletedWithSetupHandoff_CompletesParentFromBridge
PrepareSolutionSkeleton_WhenChildCompletedWithSetupHandoffAfterRepair_CompletesParentFromBridge
PrepareSolutionSkeleton_WhenChildRepairEscalation_BlocksParentWithNoGoEvidence
ObservationReader_WhenTakePerRunWouldHideStep_ExactStepQueryReturnsBlockedObservation
OperatorAction_WhenNoAFResultSummary_UsesStrategyReceiptDiagnostic
Finalization_WhenMissingExpectedOutput_DoesNotLedgerOriginalProducedArtifacts
Dispatch_WhenRequiredRuntimeToolMissing_BlocksBeforeAgentExecution
```

## Acceptance criteria

- Tests run without external LLM/network dependency.
- Tests do not use bundle names as production concepts.
- Tests fail on the current fragile behavior and pass after bundles B01-B06.
