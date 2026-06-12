# Software delivery domain proof driver extraction

## Status

- `Ready`

## Objective

Move software-delivery proof/runnable-app/.NET/JavaScript/Blazor/product-path heuristics out of the generic process dispatcher runtime and behind a verification-only domain driver or explicit domain adapter seam, while preserving the working multi-team app delivery process.

## Success Criteria

- Generic dispatcher partials no longer directly own stack-specific rules for `.NET`, `Blazor`, `Razor`, JavaScript, npm/vite/react/vue/svelte, `.csproj`, `.sln`, `.slnx`, or runnable app proof.
- The remaining dispatcher code delegates to an explicit software-delivery domain proof adapter/driver.
- The domain driver/adapter is read-only and deterministic.
- No driver performs file IO, network IO, shell execution, storage/workspace writes, process mutation, or external connector calls.
- Existing process-focused tests still pass.
- Tetris/multi-team app delivery semantics remain supported.

## Covered Inputs

- User asked whether domain drivers contain all necessary domain things or whether items remain in generic dispatcher runtime.
- Observed domain-specific code in:
  - `ProcessImplementationStackRules.cs`,
  - `ProcessRunAutomationDispatchService.ImplementationProof.cs`,
  - related concrete product path, dotnet host, receipt timeline, and carried proof rules.
- Observed `ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs` contains empty hooks while real software-delivery policy is elsewhere.

## Prerequisites

- SB01 and SB02 completed.
- Process/driver tests green before this subbundle starts.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessImplementationStackRules.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OutputValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs`
- Related files discovered by:

```bash
rg -n 'ProcessImplementationStackRules|ProcessImplementationContractSnapshot|ProcessConcreteProductPathRules|ProcessImplementationReceiptTimeline|ProcessDotNetHostEvidenceRules|ProcessCarriedImplementationProofRules|ResolveMissingRunnableApplicationProofSummary|ResolveMissingConcreteImplementationProofSummary' src/CanDoItAll.Modules.Processes src/CanDoItAll.Processes.* tests
```

## Deliverables

Preferred deliverable:

- Add a verification-only domain driver project, suggested name: `src/CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence/CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence.csproj`.
- Add it to `CanDoItAll.slnx` and reference it from `CanDoItAll.Modules.Processes.csproj`.
- Suggested package references: none.
- Suggested project references:
  - `CanDoItAll.Processes.Drivers.Abstractions`
  - `CanDoItAll.Processes.Contracts` if contract DTOs are required
  - `CanDoItAll.Processes.Core` only if descriptor types are required
- Hard forbidden references:
  - `CanDoItAll.Modules.Processes`
  - `CanDoItAll.AgentFramework.*`
  - `CanDoItAll.Infrastructure`
  - `Microsoft.EntityFrameworkCore`
  - UI/component/plugin projects

Driver/adaptor shape:

```csharp
public sealed record SoftwareDeliveryEvidenceVerificationRequest(
    ProcessDriverVerificationRequest VerificationRequest,
    ProcessDriverSuppliedEvidenceContent SuppliedContent,
    SoftwareDeliveryContractSnapshot Contract,
    IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> ToolReceipts,
    IReadOnlyList<SoftwareDeliveryExpectedArtifactSnapshot> ExpectedArtifacts,
    IReadOnlyList<SoftwareDeliveryArtifactRecordSnapshot> ArtifactRecords,
    IReadOnlyList<string> AllowedExternalTargetAliases,
    DateTimeOffset RequestedAt);
```

Move or port these rule families into the domain package with neutral DTOs:

- `SoftwareDeliveryContractRules`
- `SoftwareDeliveryStackRules`
- `SoftwareDeliveryConcreteProductPathRules`
- `SoftwareDeliveryReceiptTimelineRules`
- `SoftwareDeliveryRunnableHostEvidenceRules`
- `DotNetRunnableHostEvidenceRules`
- `SoftwareDeliveryCarriedImplementationProofRules`
- `SoftwareDeliveryImplementationProofPolicy`

Process module adapter:

- Add a small internal adapter in `CanDoItAll.Modules.Processes`, e.g. `ProcessSoftwareDeliveryEvidenceAdapter`, that maps `DispatchCandidate`, `ProcessAutomationExecutionRunDetail`, tool receipts, artifacts, work brief, expected artifacts, and allowed aliases into the driver DTOs.
- Keep this adapter as the only place where module-specific runtime models are translated for software-delivery proof policy.
- The dispatcher may keep generic methods like `ResolveMissingConcreteImplementationProofSummary`, but their bodies should delegate to the adapter/driver and not contain stack-specific rules.

Gateway integration options:

- Option A: Add explicit lane `SoftwareDeliveryEvidenceRead` and explicit method `VerifySoftwareDeliveryEvidence`. This is architecturally clean if the new driver returns `ProcessDriverVerificationResponse`.
- Option B: Keep software-delivery proof driver internal to the process module adapter for this merge, and add the gateway lane after merge. Use this only if Option A risks destabilizing the branch.
- In either option, do not add generic dispatch or runtime host behavior.

Fallback deliverable if a new project is too risky before merge:

- Create `src/CanDoItAll.Modules.Processes/Automation/Dispatch/Domain/SoftwareDelivery/**` and move all stack/product-proof rules there.
- Add tests that mark this as a pre-merge explicit exception and prevent the generic dispatcher files from regaining those rules.
- Add a roadmap note for post-merge driver lift-out.

## Dependency Impact

- SB04 depends on this subbundle to validate driver boundaries and gateway shape.
- SB05 depends on this subbundle to prove multi-team delivery behavior survived the extraction.

## Validation Depth

- Process-critical architecture extraction with focused regression proof.

## Implementation Steps

1. Capture baseline process/driver test output before editing:

```bash
dotnet test tests/CanDoItAll.Tests.Unit --filter "Process|Driver|AgentRuntimeHardeningStaticRegression"
dotnet test tests/CanDoItAll.Tests.Integration --filter Process
```

2. Inventory all domain-specific proof rules with `rg` command listed above.
3. Choose Option A unless it proves too broad; document the choice in `reviews/01-execution-report.md`.
4. Introduce neutral DTOs that do not depend on `DispatchCandidate`, EF entities, AgentFramework models, DB context, workspace storage, or process module internals.
5. Move pure static rules first; do not change behavior.
6. Add adapter mapping in the process module.
7. Replace dispatcher rule bodies with adapter calls.
8. Add source scans that fail if generic dispatcher files directly contain stack-specific proof terms outside the domain adapter/driver:

```bash
rg -n '(Blazor|Razor|dotnet|\.csproj|\.slnx|npm|pnpm|yarn|vite|react|vue|svelte|javascript|typescript)' src/CanDoItAll.Modules.Processes/Automation/Dispatch --glob '!**/Domain/SoftwareDelivery/**' --glob '!**/ProcessSoftwareDeliveryEvidenceAdapter*.cs'
```

9. Add/port unit tests for:
   - .NET runnable app proof required when contract says app/host/.csproj,
   - JavaScript/TypeScript app does not force .NET host proof,
   - negated .NET request does not force .NET proof,
   - current repair attempts require a mutation after prior proof,
   - read after latest mutation or validation after latest mutation semantics remain unchanged,
   - Tetris/Blazor-like contract still requires source/project proof and runnable host proof when appropriate.
10. Run focused tests and record output.

## Scope Exceptions

- Do not isolate the full dispatcher runtime.
- Do not move DB, lease, execution-run adoption, retry journal, or finalizer state machine logic in this subbundle.
- If Option A creates too much gateway churn, use the fallback seam and document why.

## Do Not Do

- Do not add file reads to the driver to inspect `.csproj` files. The driver must use supplied metadata/content only.
- Do not add shell commands, `dotnet` execution, Graph calls, workspace writes, storage writes, process mutation, DI discovery, or a driver registry.
- Do not remove proof checks to make tests pass.
- Do not weaken app-delivery contracts that made the Tetris run successful.

## Acceptance Checklist

- [ ] Software-delivery rules are owned by a domain driver or explicit domain adapter seam.
- [ ] Generic dispatcher partials no longer directly contain stack-specific proof terms outside allowed adapter files.
- [ ] New/updated software-delivery proof tests pass.
- [ ] Existing process/driver tests pass.
- [ ] No new MAF -> Processes reference.

## Proof Required

- Baseline and after-edit focused test outputs.
- Source scan output for forbidden stack-specific terms in generic dispatcher files.
- Project reference scan proving new driver has only allowed references.
- `dotnet test tests/CanDoItAll.Tests.Unit --filter "SoftwareDelivery|Process|Driver"`
- `dotnet test tests/CanDoItAll.Tests.Integration --filter Process`

## Browser Validation Logging

- N/A unless UI is unexpectedly touched.

## Progression Gate

SB04 may start only after the domain extraction/fallback seam is complete, tests pass, and the source scan proves stack-specific proof policy is not directly in generic dispatcher partials.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Move software-delivery proof/runnable-app/.NET/JavaScript/Blazor/product-path rules out of generic dispatcher ownership into a verification-only domain driver or explicit domain adapter seam. Preserve behavior and existing process tests. Do not introduce runtime driver hosting, DI discovery, external calls, file IO, workspace writes, or process mutation. Capture baseline tests, source scans, after-edit tests, and the chosen architecture decision.
```
