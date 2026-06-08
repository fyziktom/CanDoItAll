# Runtime Evidence Consistency Verifier Proposal

## Decision
- Defer implementation to the next bundle.
- Keep the proposed verifier read-only and descriptor-driven.
- Use immutable Core execution, finalizer, retry, and artifact projection descriptors as supplied evidence.

## Required Producer Inputs
- `repo://src/CanDoItAll.Processes.Core/Execution/ProcessExecutionEvidenceDescriptors.cs`
- `repo://src/CanDoItAll.Processes.Core/Finalization/ProcessFinalizerEvidenceDescriptors.cs`
- `repo://src/CanDoItAll.Processes.Core/Diagnostics/ProcessRetryDiagnosticDescriptors.cs`
- `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactProjectionEvidenceDescriptors.cs`

## Consumer Boundary
- The process module may collect already-produced descriptor snapshots and call a future read-only verifier explicitly.
- The verifier must return diagnostics, evidence references, redaction, audit facts, and no-mutation proof.
- The verifier must not own process lifecycle state, scheduler decisions, dispatch claims, transitions, finalizers, retries, storage writes, workspace writes, or provider repair.

## Required Negative Tests Before Implementation
- Descriptor-only evidence cannot trigger state mutation.
- Missing descriptor lineage returns diagnostics, not repair actions.
- Consumer-only tests cannot seed production-only signals without a production descriptor producer.
- Unsupported Office and business-analysis lanes remain denied.

## Next-Bundle Prerequisite
- Add production-source tests proving the descriptor producers exist and the verifier consumes only supplied descriptor payloads.
