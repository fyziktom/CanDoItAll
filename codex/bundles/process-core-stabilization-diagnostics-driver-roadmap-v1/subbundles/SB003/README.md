# SB003 - Gate A - clean baseline and warning policy

## Status
- Status: `Completed`

## Objective
Gate before Core API work.

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
- Build passes; warning count is zero or explicitly justified with narrow suppression.

## Dependency Impact
- This subbundle advances phase `P1`. Downstream work is untrustworthy if behavior parity, dependency scans, or source-boundary proof fails.

## Validation Depth
- Required proof: build + warning scan + architecture guard.

## Implementation Steps
1. Review current source before editing.
2. Make the smallest behavior-preserving production change for this subbundle.
3. Update or add architecture tests where this subbundle changes a boundary.
4. Run the subbundle's focused proof.
5. Record proof under `proof/SB003/`.

## Scope Exceptions
- Production driver APIs remain out of scope.
- Broad Process Core runtime/service extraction remains out of scope.
- UI/mobile/small/medium browser proof remains out of scope.

## Do Not Do
No continuing with hidden warning drift.

## Acceptance Checklist
- [x] Existing runtime behavior is preserved.
- [x] Core remains dependency-clean.
- [x] No production process driver API is introduced.
- [x] Source scans pass.
- [x] Proof transcript is recorded.
- [x] No UI/media files changed.

## Proof Required
- Critical proof must include `bundle://proof/SB003/manifest.md` with changed-file hashes, command transcripts, source assertions, anti-stub audit output, and portable references.
- Critical proof must include `bundle://proof/SB003/semantic-invariants.md` with Semantic Adequacy Gate coverage: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- Build or targeted test proof as appropriate.
- Architecture/source scan proof.
- For critical gates, a semantic invariant document and red-team note.

## Browser Validation Logging
- N/A runtime/service/Core refactor. If UI files change, fail this subbundle and reopen scope rather than adding mobile/small/medium proof.

## Progression Gate
- SB003 closes only when proof is recorded and downstream dependency impact is reviewed.

## Suggested Agent Prompt
Implement `SB003 - Gate A - clean baseline and warning policy` exactly as specified. Preserve all process runtime behavior. Keep production driver APIs out of scope. Record proof before moving on.

## Closure Proof
- Entry gate: Passed after SB001 and SB002 completion.
- Closure gate: Passed. See `bundle://proof/SB003/manifest.md` and `bundle://proof/SB003/semantic-invariants.md`.
- Downstream check: SB004-SB006 may proceed because the process cleanup `CA1416` warnings are gone and focused process tests are green.
