# SB06 - Artifact Descriptors Materialization And Ledger Consistency

## Status

- `Completed`
- Critical foundation: yes

## Objective

Make artifact contracts semantic and internally trustworthy: prompts/diagnostics use artifact keys/titles/primary refs, produced artifact identity is grounded in managed content, and artifact ledger events use applied finalization results.

## Covered Inputs

- F05, F06, F07.
- R04, R05, R06, R13.
- GPTPro B03.

## Prerequisites

- SB03 structured summaries complete.
- SB04 typed contract model available.

## Exact Source References

- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeArtifactContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessStepContractPromptBuilder.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`

## Deliverables

- `ProcessArtifactSlotDescriptor` and `SubprocessArtifactMappingDescriptor` or equivalent.
- Runtime step contract includes semantic descriptors.
- Prompt builder renders expectation key/title, primary managed ref, accepted child mappings, required receipts, and completion summary.
- Produced artifact refs use deterministic managed ref/content hash after readback.
- Materialization mode metadata: `AgentWritten`, `RuntimeSynthesizedParentHandoff`, `RecoveredExistingProof`, `RuntimeDiagnosticOnly`.
- `BuildArtifactLedgerEvents` uses `appliedResult`.

## Dependency Impact

- SB05 parent artifact synthesis and SB08 template hardening depend on descriptor/materialization truth. SB09 final proof depends on ledger and content-hash correctness.

## Validation Depth

- Critical foundation with semantic adequacy gate.

## Implementation Steps

1. Add descriptor records and resolver.
2. Connect descriptors into runtime step contract creation.
3. Update prompt builder to render semantic artifact details.
4. Add managed materializer/readback hashing service where appropriate.
5. Update produced artifact conversion to derive id/hash from managed ref/content.
6. Change artifact ledger helper signature to accept applied result.
7. Add tests for descriptor rendering, content hash stability, missing readback failure, and ledger downgrade.

## Scope Exceptions

- SB05 owns bridge state transitions.
- SB08 owns final template migration.

## Do Not Do

- Do not keep GUID-only output prompts.
- Do not compute artifact hashes only from raw LLM output.
- Do not ledger artifacts that finalization rejected.
- Do not silently synthesize content when readback fails.

## Acceptance Checklist

- [ ] Expected output prompt names `solution-skeleton-evidence` and primary managed ref.
- [ ] Produced artifact ref is stable for unchanged managed content.
- [ ] Produced artifact ref changes when managed content changes.
- [ ] Missing managed artifact readback is explicit failure/blocker.
- [ ] Finalization downgrade does not ledger original invalid artifacts.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- Failing-first tests for GUID-only prompt/content-free hash/invalid ledger.
- Passing tests for descriptors, content hashes, materialization modes, applied-result ledger.
- Source assertions.
- Changed-file hashes.
- Anti-stub audit.
- Production Behavior Artifact Matrix for artifact descriptor, materialization mode, and content-grounded produced artifact refs.

## Browser Validation Logging

- `N/A`.

## Progression Gate

- SB08 may migrate templates only after descriptor and ledger tests pass.

## C# Architecture Impact

Adds semantic artifact boundary and materialization seam.

## Boundary Ownership

Descriptor contracts can live in driver abstractions; materialization I/O implementation stays in module integration.

## Dependency Direction

Runtime may consume descriptors but must not reference module file I/O implementation.

## Pattern Decision

Adapter plus Builder; typed enum for materialization mode.

## Testability Contract

Tests use fake materializer/readback and verify content hash behavior without filesystem unless integration smoke is explicitly needed.

## Partial Class Policy

Do not grow adapter result conversion as final artifact materializer.

## Architecture Proof Required

- Source assertion for resolver/materializer extraction.
- Direct unit tests.
- Dependency check if references change.

## Suggested Agent Prompt

```text
Execute SB06 only. Add semantic artifact descriptors, content-grounded produced artifact refs, materialization modes, and applied-result ledger. Do not edit all templates except fixtures needed for tests.
```
