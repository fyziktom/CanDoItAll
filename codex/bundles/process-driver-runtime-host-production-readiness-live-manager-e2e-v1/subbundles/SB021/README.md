# SB021 - Critical Gate G: process runtime uses verification host only as read-only observer

## Status
- Status: Completed
- Closure evidence: bundle://reviews/01-execution-report.md

## Objective
Critical Gate G: process runtime uses verification host only as read-only observer

## Covered Inputs
- Raw request asks for real-code verification, real test classification, and next phases toward a stable generic Process Core with domain drivers.

## Prerequisites
- Complete all prior subbundles in numeric order.
- Re-read current branch source before changing files.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Unit`

## Deliverables
- Source changes or source-backed proof for this subbundle objective.
- Updated tests and proof transcripts.
- Updated execution report row for this exact subbundle.

## Dependency Impact
- Downstream phases must not proceed if this subbundle weakens read-only verification boundaries, Process Core genericity, durable audit, or process runtime launch/execution proof.

## Validation Depth
- Critical foundation: requires Semantic Adequacy Gate, production behavior artifact matrix, changed-file hashes, command transcripts, source assertions, anti-stub audit, and red-team negative proof.

## Implementation Steps
1. Re-read the source references listed above.
2. Implement the smallest coherent change that satisfies the objective.
3. Add or update focused tests.
4. Run targeted tests and source scans.
5. Record proof under `proof/SB021/` if this is a critical gate, otherwise under the nearest phase proof area.

## Scope Exceptions
- Do not implement execution-capable drivers in this subbundle.
- Do not use small/medium/mobile UI proof.

## Do Not Do
- Do not add shell/package restore/Graph/file/network/workspace/storage/process mutation through drivers.
- Do not add fallback lane selection.
- Do not use concrete `codex/bundles/<name>` paths in long-lived source/tests.
- Do not report skipped live tests as live provider proof.

## Acceptance Checklist
- [x] Source compiles.
- [x] Focused tests pass.
- [x] Source scans pass for forbidden runtime authority and bundle-path coupling.
- [x] Execution report has an individual row for `SB021`.
- [x] Semantic Adequacy Gate proof exists for this critical gate.

## Proof Required
- Completed critical proof: bundle://proof/SB021/manifest.md and bundle://proof/SB021/semantic-invariants.md
- Command transcript paths.
- Source assertion transcript.
- Anti-stub/runtime-authority scan.
- Red-team negative proof for critical gate.

## Browser Validation Logging
- N/A unless browser-visible source changes unexpectedly.

## Progression Gate
- Proceed only after this subbundle has source-backed proof and downstream dependency impact reviewed.

## Suggested Agent Prompt
Implement `SB021` for `process-driver-runtime-host-production-readiness-live-manager-e2e-v1`. Preserve read-only verification boundaries, use real source/tests, and do not collapse proof into report-only claims.


