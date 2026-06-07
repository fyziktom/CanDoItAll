# Finalizer Evidence Descriptor Inventory

## Scope
- Covers SB007 finalizer intent and result facts for workflow, manager recovery, direct agent, and subprocess completion.
- Separates Core-safe descriptor facts from module-owned finalizer execution and transition application.
- Feeds SB008 implementation and SB009 parity proof.

## Pure Core Descriptor Fields
- Finalizer intent: finalizer kind, process run id, step run id, completion status, completion reason, selected branch outcome id, execution/workflow/subprocess ids, projection flags, trigger, lease-renewal requirement, recovery execution run id, and recovered-for execution run id.
- Finalizer result: result presence, transition-application decision, completion status/reason, Core block-cause classification, selected branch outcome id, step run concurrency token, and artifact validation result count.
- Derived classifications: should-apply transition, result/no-result state, artifact-validation presence, finalizer kind, and block-cause kind.

## Module-Owned Runtime Fields
- Finalizer delegate invocation, route claim adaptation, lease renewal callback, transition mutation, and persistence.
- Artifact validation orchestration, artifact projection/writeback, block-cause module enum, and transition request application.
- Workflow/recovery/direct/subprocess route handler orchestration.
- Process driver proposal/registry/selector concepts, which remain out of production scope for this bundle.

## Adapter Ownership
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessFinalizerEvidenceDescriptorAdapter.cs` is the finalizer descriptor bridge.
- The adapter maps module finalizer context and result facts into Core descriptors.
- `ProcessDispatchFinalizerAdapter` still owns null-result no-apply and apply-on-result behavior.

## Validation
- Source assertions: `bundle://proof/SB009/transcripts/source-assertions.txt`.
- Behavioral proof: `bundle://proof/SB009/transcripts/finalizer-descriptor-focused-integration-tests.txt`.
- Boundary scan: `bundle://proof/SB009/transcripts/adapter-confinement-scan.txt`.
- Gate semantics: `bundle://proof/SB009/semantic-invariants.md`.
