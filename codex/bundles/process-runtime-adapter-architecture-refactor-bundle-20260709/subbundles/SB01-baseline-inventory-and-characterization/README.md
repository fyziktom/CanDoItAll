# SB01 - Baseline Inventory And Characterization

## Status

- Status: `Completed`

## Objective

Establish behavior and architecture baseline before production refactoring.

## Covered Inputs

- User request to refactor the adapter without implementing during preparation.
- GPTPro root-cause findings around branch gates, repair loopbacks, and domain leaks.
- Current adapter partial-class inventory.

## Prerequisites

- Bundle README and architecture files read.
- CodeAnalytics available or explicit gap recorded.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptWriter.cs`
- `repo://codex/bundles/tetris-process-rootcause-workflow-bundle-20260709`
- `repo://codex/bundles/escalation_root_cause_bundle`

## Dependency Impact

- No production dependency changes expected.

## Validation Depth

- Characterization tests plus source assertions.
- This phase must not rely only on file existence.

## Do Not Do

- Do not move production behavior yet.
- Do not add partial files.
- Do not weaken gates.

## Acceptance Checklist

- [ ] Adapter partial baseline captured.
- [ ] Domain-term baseline captured.
- [ ] Characterization tests added.
- [ ] CodeAnalytics baseline recorded.

## Proof Required

- Proof manifest with test transcript.
- Source assertions.
- CodeAnalytics snapshot id.

## Browser Validation Logging

- Not applicable unless a process E2E browser validation is explicitly added as characterization.

## Progression Gate

- SB02 may start only after baseline tests and source assertions exist.

## Suggested Agent Prompt

Implement SB01 only. Capture baseline behavior and do not refactor production code beyond test/support changes needed for characterization.

## Goal

Create a precise baseline before moving behavior. This subbundle must lock observable behavior, record adapter partial responsibility ownership, record domain-leak locations, and establish source assertions that later subbundles will improve.

## Scope

- Inventory `AgentFrameworkProcessExecutionAdapter` partial files and responsibilities.
- Inventory generic runtime/dispatcher/MAF files containing domain terms.
- Add characterization tests for adapter completion gates, receipt matching, managed artifact behavior, subprocess propagation, and receipt writer summaries.
- Capture CodeAnalytics baseline dependency/cycle result.

## Implementation Steps

1. Record current `AgentFrameworkProcessExecutionAdapter.*.cs` files, line counts, and key method groups.
2. Add source assertion script or test for adapter partial file count.
3. Add source assertion script or test for forbidden generic domain terms with explicit allowed exceptions.
4. Add characterization tests around current completion gate outcomes.
5. Add characterization tests around `ProcessRequiredToolReceiptGate`.
6. Add characterization tests around managed artifact materialization/acceptance behavior.
7. Add characterization tests around parent subprocess blocked-child propagation.
8. Add characterization tests for `WorkspaceCommandReceiptWriter` request summary behavior, including current .NET lifecycle facts, before extraction.
9. Capture CodeAnalytics scoped snapshot and dependency result.
10. Update proof manifest.

## C# Architecture Impact

This subbundle should not perform the refactor yet. It creates the safety net for later extraction and prevents behavior loss.

## Boundary Ownership

No project ownership changes expected. If tests require new test helper types, keep them in test projects.

## Dependency Direction

No production project reference changes expected. CodeAnalytics baseline must be captured for comparison.

## Pattern Decision

No new production pattern. Tests should be written around current behavior and later mapped to PSR-1 through PSR-4.

## Testability Contract

Characterization tests may instantiate the current adapter only when unavoidable for current behavior. The test names must clearly mark them as characterization. Later subbundles must replace those with direct extracted-service tests.

Required tests:

- Gate aggregation/current behavior cases.
- Receipt current-run and branch applicability cases.
- Managed artifact read/write/readback cases.
- Subprocess child blocked propagation case.
- Receipt writer lifecycle summary case.

## Partial Class Policy

No new partial files. Baseline must document existing partials as temporary debt.

## Architecture Proof Required

- Adapter partial file list and line count.
- Domain term source assertion baseline.
- CodeAnalytics snapshot id and cycle result.
- Characterization test transcript.
- Statement that no production source refactor was performed in SB01 except test/support artifacts.
