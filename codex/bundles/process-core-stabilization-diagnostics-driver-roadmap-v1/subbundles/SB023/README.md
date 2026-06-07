# SB023 - Core dependency guard hardening

## Status
- Status: `Completed`

## Objective
Tighten regex scans for forbidden namespaces/tokens and package refs.

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
- No EF/AgentFramework/storage/workspace/driver tokens in Core.

## Dependency Impact
- This subbundle advances phase `P8`. Downstream work is untrustworthy if behavior parity, dependency scans, or source-boundary proof fails.

## Validation Depth
- Required proof: architecture tests.

## Implementation Steps
1. Review current source before editing.
2. Make the smallest behavior-preserving production change for this subbundle.
3. Update or add architecture tests where this subbundle changes a boundary.
4. Run the subbundle's focused proof.
5. Record proof under `proof/SB023/`.

## Scope Exceptions
- Production driver APIs remain out of scope.
- Broad Process Core runtime/service extraction remains out of scope.
- UI/mobile/small/medium browser proof remains out of scope.

## Do Not Do
No production driver tokens.

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
- SB023 closes only when proof is recorded and downstream dependency impact is reviewed.

## Closure Proof
- Result: `Passed`
- Manifest: `bundle://proof/SB023/manifest.md`
- Focused transcript: `bundle://proof/SB023/transcripts/core-dependency-guard-hardening-test.txt`
- Core forbidden dependency scan: `bundle://proof/SB024/transcripts/core-forbidden-dependency-scan.txt`
- Core project reference scan: `bundle://proof/SB024/transcripts/core-project-reference-scan.txt`

## Suggested Agent Prompt
Implement `SB023 - Core dependency guard hardening` exactly as specified. Preserve all process runtime behavior. Keep production driver APIs out of scope. Record proof before moving on.
