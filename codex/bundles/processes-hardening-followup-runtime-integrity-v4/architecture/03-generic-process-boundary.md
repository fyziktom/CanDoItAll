# Generic Process Boundary

This hardening must not become software-specific.

## Generic Operations

- `ReadProcessContext`
- `ReadProjectStructure`
- `ReadUpstreamArtifacts`
- `WriteManagedProcessArtifacts`
- `WriteExternalArtifactDestination`
- `MutateTargetState`
- `RunValidation`
- `LaunchRuntimeOrInspection`
- `CaptureRuntimeProof`
- `ExecuteExternalAction`
- `RecoverArtifactsOnly`
- `EscalateOrDecide`

`MutateTargetState` is intentionally broader than software mutation. It can mean:

- editing source files
- changing a business plan document
- sending a purchase order
- updating a manufacturing schedule
- modifying a legal contract draft
- generating a dataset
- writing a spreadsheet/deck/report to an external destination

## Target Kinds

- `ManagedProcessArtifact`
- `ManagedGeneratedOutput`
- `ExternalArtifactDestination`
- `ExternalProductOrBusinessTarget`
- `ExternalSystemAction`
- `ReadOnlyEvidenceSource`

## Principle

A process step may write its own process artifacts without being allowed to mutate the real target. A step must have explicit target mutation permission to modify the deliverable, business object, system state, repository, external folder, or other target.
