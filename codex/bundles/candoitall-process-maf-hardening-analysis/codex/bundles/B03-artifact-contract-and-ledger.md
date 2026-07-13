# B03 — Artifact contract and ledger hardening

## Goal

Make artifact contracts semantically useful to agents and internally trustworthy for the runtime.

## Required changes

### 1. Add semantic artifact descriptors

Extend or wrap the existing slot refs with descriptors such as:

```csharp
public sealed record ProcessArtifactSlotDescriptor(
    ArtifactSlotId SlotId,
    string ExpectationKey,
    string Title,
    string StepKey,
    string StepTitle,
    string? SourceStepKey,
    string? SourceStepTitle,
    string PrimaryManagedRef,
    IReadOnlyList<string> AdditionalAllowedRefs,
    bool IsRequired,
    string ValidationRequirementSummary);
```

For subprocess outputs add:

```csharp
public sealed record SubprocessArtifactMappingDescriptor(
    string ParentArtifactExpectationKey,
    string ParentArtifactTitle,
    string ChildStepKey,
    string ChildArtifactExpectationKey,
    string ChildArtifactTitle,
    bool IsAccepted,
    bool IsNoGo);
```

### 2. Render descriptors in prompts

Update `ProcessStepContractPromptBuilder` so it does not only print GUID slots.

For expected output, render something like:

```text
Expected output artifacts:
- solution-skeleton-evidence / Solution skeleton evidence
  Primary managed ref: artifacts/process-runs/<run>/steps/prepare-solution-skeleton.md
  Completion rule: must cite accepted child setup handoff or already-existing skeleton proof.
```

### 3. Use actual managed artifact content hash

`AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` currently creates produced artifacts with `ArtifactInstanceId.New()` and a hash derived from raw output. Replace this with deterministic materialization/readback:

- resolve primary managed ref,
- read content after write/synthesis,
- hash actual content,
- create stable artifact id from run/step/slot/ref/content hash.

### 4. Fix artifact ledger events

Change:

```csharp
BuildArtifactLedgerEvents(resultEventId, command)
```

to use `appliedResult`:

```csharp
BuildArtifactLedgerEvents(resultEventId, appliedResult)
```

and update helper signature accordingly.

Do not ledger artifacts that were produced only by an original result that finalization downgraded to `NeedsManager`.

### 5. Add materialization modes

Introduce a small enum or metadata field:

- `AgentWritten`
- `RuntimeSynthesizedParentHandoff`
- `RecoveredExistingProof`
- `RuntimeDiagnosticOnly`

Use it in diagnostics and managed artifact headers.

## Tests

- `RuntimeContractPrompt_IncludesArtifactKeysTitlesAndPrimaryManagedRefs`
- `ProducedArtifactRef_UsesManagedArtifactContentHash`
- `ProducedArtifactRef_IsStableForSameManagedContent`
- `Finalization_WhenOutputMissing_DowngradesAndDoesNotLedgerInvalidProducedArtifact`
- `ParentBridgeArtifact_HasRuntimeSynthesizedMaterializationMode`

## Acceptance criteria

- Agent-facing prompts are understandable without resolving GUID slot IDs.
- Runtime ledger reflects the applied result, not the original invalid result.
- Downstream steps can trace an available slot to a concrete managed artifact file and content hash.
