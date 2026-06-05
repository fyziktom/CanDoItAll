# Finalizer Method Classification Template

Codex must fill this from source in SB02 before production movement.

| Method / region | File | Category | Inputs | Outputs | Side effects | Candidate extraction | Must preserve |
| --- | --- | --- | --- | --- | --- | --- | --- |
| finalizer enums | StepCompletionFinalizer | value vocabulary | none | enum values | none | yes | names and mappings |
| `IProcessArtifactContentReader` and implementations | StepCompletionFinalizer | content read boundary | managed storage path | content read result | file/storage reads | yes | fallback and diagnostics |
| `FinalizeStepCompletionAsync` | StepCompletionFinalizer | orchestration | finalizer context | finalizer result | projection/recovery/audit calls | no wholesale move | ordering |
| `ApplyFinalizedStepTransitionAsync` | StepCompletionFinalizer | transition mutation | finalizer result | step transition | DB transition/cost sync | keep dispatcher-owned | concurrency/stale handling |
| `BuildStepTransitionArtifactValidationContext` | StepCompletionFinalizer | pure mapping | finalizer context | validation context | none | yes | all IDs |
| `ApplyArtifactValidationContext` | StepCompletionFinalizer | pure mapping | transition request/context | mutated request | request mutation | yes as builder | all fields |
| `ValidateRequiredCompletionArtifactsAsync` | StepCompletionFinalizer | validation orchestration | context | validation results | EF read/content read | partial | result order/satisfaction |
| `PersistRuntimeInvariantAuditAsync` | StepCompletionFinalizer | invariant audit/persistence | context/results | violations | EF read/persist | split build vs persist | severe blocking |
