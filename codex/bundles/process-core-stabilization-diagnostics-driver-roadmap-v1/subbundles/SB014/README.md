# SB014 - Module transition intent adapter

## Status
- Status: `Completed`

## Objective
Map Core transition intents to module transition requests.

## Covered Inputs
- Raw user request: continue toward a complete stable Process Core and future domain drivers.
- Current branch state after `process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1`.

## Prerequisites
- All previous subbundles in phase order must be complete.
- If this is a critical gate, all earlier phase proof must be green.

## Exact Source References
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1`

## Deliverables
- Adapter owns ProcessStepTransitionRequest construction.

## Dependency Impact
- This subbundle advances phase `P5`. Downstream work is untrustworthy if behavior parity, dependency scans, or source-boundary proof fails.

## Validation Depth
- Required proof: subprocess/start transition integration.

## Implementation Steps
1. Review current source before editing.
2. Make the smallest behavior-preserving production change for this subbundle.
3. Update or add architecture tests where this subbundle changes a boundary.
4. Run the subbundle's focused proof.
5. Record proof under `proof/SB014/`.

## Scope Exceptions
- Production driver APIs remain out of scope.
- Broad Process Core runtime/service extraction remains out of scope.
- UI/mobile/small/medium browser proof remains out of scope.

## Do Not Do
Do not move claims or EF.

## Acceptance Checklist
- [x] Existing runtime behavior is preserved.
- [x] Core remains dependency-clean.
- [x] No production process driver API is introduced.
- [x] Source scans pass.
- [x] Proof transcript is recorded.
- [x] No UI/media files changed.

## Proof Required
- Build or targeted test proof as appropriate.
- Architecture/source scan proof.
- For critical gates, a semantic invariant document and red-team note.

## Browser Validation Logging
- N/A runtime/service/Core refactor. If UI files change, fail this subbundle and reopen scope rather than adding mobile/small/medium proof.

## Progression Gate
- SB014 closes only when proof is recorded and downstream dependency impact is reviewed.

## Closure Proof
- Result: `Passed`
- Manifest: `bundle://proof/SB014/manifest.md`
- Start-transition transcript: `bundle://proof/SB014/transcripts/module-transition-intent-adapter-test.txt`
- Subprocess transition transcript: `bundle://proof/SB014/transcripts/subprocess-transition-intent-adapter-test.txt`
- Gate proof: `bundle://proof/SB015/manifest.md`

## Suggested Agent Prompt
Implement `SB014 - Module transition intent adapter` exactly as specified. Preserve all process runtime behavior. Keep production driver APIs out of scope. Record proof before moving on.
