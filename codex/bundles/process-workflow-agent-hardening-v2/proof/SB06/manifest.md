# SB06 Proof Manifest

## Scope

Subbundle: `SB06 Process dispatch heuristic refactor`.

This pass extracts dispatch decision seams into typed nested services while preserving existing dispatch behavior under targeted characterization tests. The change is intentionally scoped to the existing partial dispatch service boundary to avoid introducing a new dependency graph for a private orchestration implementation.

## Source Changes

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DecisionServices.cs`
  - Adds typed resolver/policy/decision boundaries: `IRequiredToolResolver`, `IBrowserProofRequirementResolver`, `IArtifactRequirementMatcher`, `IStepCompletionPolicy`, and `IDispatchDecisionEngine`.
  - Adds typed decision/diagnostic records used by those boundaries.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
  - Makes carried implementation proof visible to the typed decision service boundary.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
  - Routes existing required-tool, browser-proof, artifact matching, completion, and retry decisions through typed service members.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
  - Adds typed-boundary coverage and preserves existing dispatch golden slices.

Changed file hashes:

- `bundle://proof/SB06/changed-file-hashes.txt`

## Passing Proof

- `bundle://proof/SB06/transcripts/passing-dispatch-decision-services.txt`
  - `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~DispatchDecisionServices_expose_typed_resolver_boundaries|FullyQualifiedName~ResolveRequiredToolNames_for_dotnet_solution_setup_scaffold_step_skips_runtime_validation|FullyQualifiedName~ResolveRequiredToolNames_for_console_setup_validation_does_not_require_browser_tools|FullyQualifiedName~ResolveCompletionStatusWithCarryForward_allows_explicit_repair_disposition_despite_failed_diagnostic_tool|FullyQualifiedName~ShouldRetryIncompleteSuccessfulRun_returns_false_for_explicit_repair_disposition_with_failed_diagnostic_tool" --no-restore`
  - Result: 5/5 passed.

## Anti-Stub Audit

- `bundle://proof/SB06/anti-stub-audit.txt`
  - Scanned dispatch decision service files and tests for TODO, NotImplemented, and stub-only markers.
  - Result: pass.

## Raw Note Closure

SB06 closes the raw-note slice for process dispatch heuristic refactor by making the resolver/policy boundaries explicit and test-addressable without changing process execution semantics.

## Downstream Impact

SB08 live browser proof and SB09 completed-stage validation were run after this refactor, exercising downstream process state and workflow UI surfaces against the refactored dispatch service boundary.
