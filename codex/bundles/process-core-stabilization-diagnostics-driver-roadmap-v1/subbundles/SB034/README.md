# SB034 - Core readiness scorecard vNext

## Status
Prepared.

## Objective
Update scorecard with exact next candidate.

## Covered Inputs
- Raw user request: continue toward a complete stable Process Core and future domain drivers.
- Current branch state after `process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1`.

## Prerequisites
- All previous subbundles in phase order must be complete.
- If this is a critical gate, all earlier phase proof must be green.

## Exact Source References
- `src/CanDoItAll.Processes.Core/`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/`

## Deliverables
Decide whether next bundle expands Core or starts driver contracts proposal.

## Dependency Impact
This subbundle advances phase `P12`. Downstream work is untrustworthy if behavior parity, dependency scans, or source-boundary proof fails.

## Validation Depth
Required proof: scorecard.

## Implementation Steps
1. Review current source before editing.
2. Make the smallest behavior-preserving production change for this subbundle.
3. Update or add architecture tests where this subbundle changes a boundary.
4. Run the subbundle's focused proof.
5. Record proof under `proof/SB034/`.

## Scope Exceptions
- Production driver APIs remain out of scope.
- Broad Process Core runtime/service extraction remains out of scope.
- UI/mobile/small/medium browser proof remains out of scope.

## Do Not Do
No ambiguous broad approval.

## Acceptance Checklist
- [ ] Existing runtime behavior is preserved.
- [ ] Core remains dependency-clean.
- [ ] No production process driver API is introduced.
- [ ] Source scans pass.
- [ ] Proof transcript is recorded.
- [ ] No UI/media files changed.

## Proof Required
- Build or targeted test proof as appropriate.
- Architecture/source scan proof.
- For critical gates, a semantic invariant document and red-team note.

## Browser Validation Logging
N/A runtime/service/Core refactor. If UI files change, fail this subbundle and reopen scope rather than adding mobile/small/medium proof.

## Progression Gate
SB034 closes only when proof is recorded and downstream dependency impact is reviewed.

## Suggested Agent Prompt
Implement `SB034 - Core readiness scorecard vNext` exactly as specified. Preserve all process runtime behavior. Keep production driver APIs out of scope. Record proof before moving on.
