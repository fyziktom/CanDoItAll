# SB09 DotNet Runtime Lifecycle Hardening

## Status

- `Completed`

## Objective

Harden .NET runtime run/stop evidence so browser/runtime receipts prove the correct product lifecycle and do not leave orphaned hosts, stale ports, or misleading receipt records.

## Covered Inputs

- GPTPro MAF wrapper and lifecycle notes.
- User escalation about runtime proof contributing to loopbacks.
- Requirement R09.

## Prerequisites

- SB03 branch-aware receipt enforcement is available or its target contract is stable.
- Runtime command templates and .NET tool lifecycle surfaces are identified.
- Existing runtime cancellation observer tests are reviewed.

## Exact Source References

- `bundle://codex-tasks/09-dotnet-runtime-tool-lifecycle.md`
- `bundle://08-maf-wrapper-and-tool-lifecycle-notes.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeOwnedStepCoordinator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/Drivers/DotNet/DotNetSolutionSetupRuntimeExecutor.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeToolPreflightService.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFrameworkProcessRuntimeCancellationObserverTests.cs`
- `repo://Templates/Processes/processes/dotnet-runtime-command-writeback/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`

## Deliverables

- Lifecycle state contract for run, observe, stop, timeout, and cleanup.
- Receipt metadata tying browser proof to the active host and product root.
- Tests for normal stop, timeout stop, orphan detection, stale receipt rejection, and port mismatch.
- Template changes where runtime writeback receipts need branch or lifecycle applicability.

## Dependency Impact

- SB07 template migration depends on accurate runtime receipt semantics.
- SB10 diagnostics depend on lifecycle state being observable.
- SB11 final regression depends on lifecycle tests passing.

## Validation Depth

- Runtime reliability phase.
- Requires fake host tests and at least one integration-style lifecycle transcript.

## Implementation Steps

1. Map current lifecycle states and receipt generation points.
2. Add or refine typed lifecycle state values for active, observed, stopped, failed, timeout, and orphaned.
3. Tie runtime/browser receipts to product root, command identity, host process, port, and branch applicability.
4. Reject stale receipts where the host is not current for the assignment.
5. Add cleanup behavior with explicit logged state when stop fails.
6. Update runtime writeback templates if they need lifecycle-aware receipt rules.
7. Add tests for stale receipt and orphaned host negative cases.

## C# Architecture Impact

This phase keeps runtime lifecycle behavior in runtime integration code, not in process prompts.

## Boundary Ownership

- Process runtime integration owns lifecycle state and receipts.
- Templates own when lifecycle evidence is required.
- Workbench only displays or launches through established runtime APIs.

## Dependency Direction

- Runtime lifecycle code can depend on generic process contracts.
- Generic process contracts must not reference concrete .NET tool command names unless the contract is explicitly tool-agnostic.

## Pattern Decision

- Explicit lifecycle state model with receipt correlation.
- Rejected: implicit "browser screenshot exists" acceptance.

## Testability Contract

- Fake process/host abstractions must allow lifecycle tests without spawning real app hosts.
- Integration proof can cover one real or simulated end-to-end path.

## Partial Class Policy

- Avoid adding adapter partials as final architecture.
- If partial files are touched, SB11 must verify an extraction path or justify the local edit.

## Architecture Proof Required

- Source assertion for typed lifecycle state.
- Negative tests for stale host and orphan cleanup.
- Log assertion proving lifecycle failures include assignment, branch, product root alias, and masked command state.

## Do Not Do

- Do not silently ignore host stop failures.
- Do not accept a browser receipt that cannot be correlated to the active product host.
- Do not store unmasked sensitive command arguments in logs.

## Acceptance Checklist

- Runtime receipts can be correlated to the current assignment and host.
- Stale browser/runtime receipts fail predictably.
- Stop failures are logged with actionable state.
- Runtime writeback templates remain compatible with branch-aware receipts.

## Proof Required

- `bundle://proof/SB09/manifest.md` after execution.
- Lifecycle failing-first and passing transcripts.
- Stale receipt and orphan cleanup test transcripts.
- Source assertions for correlation fields and masked logs.
- Anti-stub audit for fake host tests.

## Browser Validation Logging

- Browser validation is required when an end-to-end runtime host is exercised.
- Record host identity alias, route, viewport, evidence path, screenshot path, and lifecycle result in `reviews/01-execution-report.md`.

## Progression Gate

- SB11 final closure must not accept browser/runtime receipt fixes until stale receipt and stop failure tests pass.

## Suggested Agent Prompt

Implement SB09 by making .NET runtime receipts lifecycle-aware and correlated to the active assignment host. Add fake host tests for stale receipts, orphan cleanup, and explicit stop failure diagnostics.
