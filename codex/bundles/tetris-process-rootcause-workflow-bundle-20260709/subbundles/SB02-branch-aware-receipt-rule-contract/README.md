# SB02 Branch-Aware Receipt Rule Contract

## Status

- `Completed`

## Objective

Introduce structured branch-aware receipt rule parsing and launch-variable preservation while keeping all legacy receipt formats working.

## Covered Inputs

- GPTPro RC1 and RC3.
- GPTPro Task 03.
- Requirement R01.

## Prerequisites

- SB01 extraction is complete or has a stable evaluator seam.
- Legacy receipt tests are identified.
- No template migration starts before parser compatibility is proven.

## Exact Source References

- `bundle://02-root-causes.md`
- `bundle://04-target-architecture.md`
- `bundle://codex-tasks/03-branch-aware-receipt-rules.md`
- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessLaunchExecutorResolver.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeStepAssignmentRepairService.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessCapabilityScopeContractTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRequiredRuntimeToolNamesTests.cs`

## Deliverables

- Typed rule model for product completion required tool receipts or extended existing receipt model with branch/purpose fields.
- Parser supporting legacy string, JSON string array, JSON object array, by-step string map, and by-step object map.
- Tool-name extraction from object rules for launch capability resolution.
- Step-scoped launch variable formatting that preserves object rules instead of flattening them.
- Backward compatibility tests for existing templates.

## Dependency Impact

- SB03 branch-aware enforcement requires structured rules.
- SB07 template migration cannot emit object rules until SB02 is complete.

## Validation Depth

- Critical foundation.
- Requires parser matrix tests and launch preservation tests.

## Implementation Steps

1. Choose whether to extend `ProcessRequiredToolReceipt` or add a product-completion-specific rule record.
2. Add fields for `Purpose`, `EnforceBranchOutcomeKeys`, `SkipBranchOutcomeKeys`, `RequireCurrentRun`, `RequireSuccessfulExit`, `MinimumCount`, and `Reason`.
3. Parse object arrays using structured JSON APIs, not regex or string splitting.
4. Preserve object arrays when resolving by-step launch variables.
5. Ensure active tool-name discovery reads object rules.
6. Add tests for every legacy and new format.
7. Verify existing process templates still load.

## C# Architecture Impact

This phase adds generic contracts. The fields are branch/purpose metadata, not software-delivery semantics.

## Boundary Ownership

- Contracts own generic rule shape.
- Workbench/templates later own domain-specific values.

## Dependency Direction

- Contracts must remain dependency-free.
- Application/modules may consume contract types.

## Pattern Decision

- Parser/normalizer service or static contract helper.
- Rejected: ad hoc string manipulation over JSON.

## Testability Contract

- Parser tests must not require process runtime construction.
- Launch variable preservation tests must prove object rules survive step resolution.

## Partial Class Policy

- No adapter partial expansion for parser logic.
- Parser belongs outside the adapter partial cluster.

## Architecture Proof Required

- Source assertion that object parsing is typed.
- Source assertion that launch variable formatting preserves structured JSON.
- No domain-specific branch names in parser logic except test fixture data.

## Do Not Do

- Do not change gate enforcement semantics yet.
- Do not migrate templates in this subbundle.
- Do not require all templates to use object rules immediately.

## Acceptance Checklist

- Legacy newline strings parse.
- Legacy JSON string arrays parse.
- Legacy by-step string maps parse.
- JSON object arrays parse.
- By-step object maps parse and preserve object fields.
- Tool names extract from object rules.

## Proof Required

- `bundle://proof/SB02/manifest.md` after execution.
- `bundle://proof/SB02/semantic-invariants.md` after execution.
- Failing-first parser test transcript if behavior currently drops object rules.
- Passing parser/launch preservation transcript.
- Source assertion transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A for SB02; no browser-visible behavior.

## Progression Gate

- SB03 and SB07 may start only after structured object rules parse and legacy formats remain green.

## Suggested Agent Prompt

Implement SB02 by adding typed branch-aware receipt rule parsing and preserving structured rules through launch-variable resolution. Keep all legacy behavior compatible.
