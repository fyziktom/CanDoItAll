# 07 Refactor Operational Proof And UI Safety

## Status

- Status: `Ready for implementation`

## Objective

Complete maintainability refactors, service wiring, migrations, UI proof, and final build/test/browser validation for the full follow-up bundle.

## Covered Inputs

- F-10 tests prove plumbing, not quality.
- RQ-13 final refactor and proof.
- All downstream proof closure.

## Prerequisites

- SB01 through SB06 must be implemented or explicitly blocked with rationale.
- No critical gate may remain ambiguous.

## Exact Source References

- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryCuratorTab.razor
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Quality.cs
- /mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryClusterSearchTab.razor
- /mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- /mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- /mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs

## Deliverables

- Service refactors from `architecture/03-refactor-map.md` where needed to keep code testable.
- DI registration, persistence/migration updates, snapshot/view model updates, and UI binding updates.
- Updated execution report with every subbundle gate result.
- Clean build and targeted/full test proof.
- Browser/component proof for all UI-visible changes.

## Dependency Impact

- Final closure depends on this subbundle.
- Future economic governance work should start only after this proof is stable.

## Validation Depth

- Clean build.
- Targeted Cognitive Memory unit tests.
- Relevant component tests.
- Browser/Playwright proof for changed Cognitive Memory tabs.
- Manual review of screenshots for readability and no reference overload.

## Implementation Steps

- Extract services carefully without changing behavior beyond already proven subbundle requirements.
- Update dependency injection and tests for new services.
- Run build and targeted tests.
- Run component/browser validation for curator/quality/cluster search UI if changed.
- Fill execution report tables and raw note closure.
- Confirm RQ-14 economic governance exclusion.

## Scope Exceptions

- If full test suite is too large, record targeted tests plus rationale and provide a clean build.
- Do not leave critical P0 regressions as skipped tests without explicit blocker.

## Do Not Do

- Do not introduce last-minute behavior changes without tests.
- Do not close the bundle with pending browser analytics for UI-visible changes.

## Acceptance Checklist

- All critical subbundle gates are closed or explicitly blocked.
- Build/test proof is recorded.
- UI-visible changes have browser/component evidence.
- Execution report is complete enough for a CTO review.

## Proof Required

- `dotnet build` or solution-level equivalent.
- Targeted `dotnet test` outputs.
- Component/browser evidence paths.
- Completed execution report.

## Browser Validation Logging

- Route: `/cognitive-memory` changed tabs.
- Large desktop viewport plus responsive smoke for changed layout.
- Capture screenshots for curator target controls, cluster metrics, dream validation warnings, and reference expansion where implemented.

## Progression Gate

- Final bundle closure only when all previous gates and proof rows are complete.

## Suggested Agent Prompt

Finalize the Cognitive Memory follow-up implementation with maintainability refactors, DI/migration/UI wiring, clean build/tests, browser proof, and completed execution report. Do not implement economic governance.
