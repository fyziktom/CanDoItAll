# SB056 — Prepared/completed validator preflight

## Status
Prepared.

## Objective
Execute the prepared/completed validator preflight slice for **P19: Final red-team and fake-proof resistance**.

## Covered Inputs
- Raw request for real-code review, live OpenAI proof, and progress toward generic Process Core with domain drivers.
- Normalized requirements in `bundle://requirements/01-normalized-requirements.md`.

## Prerequisites
- Previous subbundle gate must pass.
- For critical subbundles, all upstream source assertions and transcripts must exist before implementation continues.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Drivers.*`
- `repo://tests/CanDoItAll.Tests.Unit`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Playwright`
- `bundle://analysis/01-current-state-review.md`
- `bundle://architecture/01-target-architecture.md`

## Deliverables
- Source changes only if needed for this slice.
- Tests proving the behavior and rejecting the shallow/unsafe alternative.
- Proof artifacts under `bundle://proof/SB056/`.

## Dependency Impact
This subbundle belongs to P19. Its output feeds downstream phases in `bundle://plan/01-phase-plan.md`. If this proof is wrong, later host/manager/runtime conclusions are untrustworthy.

## Validation Depth
Focused implementation proof plus source assertions feeding the next critical gate.

## Implementation Steps
1. Re-read the exact source files before editing.
2. Implement the smallest complete source change for this slice.
3. Add or update focused tests.
4. Run the scoped tests and source scans.
5. Record proof transcripts, changed-file hashes, and source assertions.
6. Update `reviews/01-execution-report.md` only after proof exists.

## Scope Exceptions
- Execution-capable drivers remain out of scope unless the subbundle explicitly says otherwise; no subbundle in this bundle approves them.
- Small/medium/mobile UI proof is out of scope.

## Do Not Do
- Do not add generic object dispatch.
- Do not add shell execution, package restore, Office/Graph calls, workspace/storage writes, or process mutation through drivers.
- Do not let Process Core reference drivers/modules/infrastructure/EF/UI/AgentFramework.
- Do not log OpenAI/API secrets.
- Do not depend on concrete transient `codex/bundles/<bundle-name>` paths in long-lived source/tests.

## Acceptance Checklist
- [ ] Source code matches the allowed boundary.
- [ ] Tests include positive and negative cases.
- [ ] Build/test commands are captured.
- [ ] Source scans prove no forbidden drift.
- [ ] No secrets are printed.
- [ ] Execution report row is updated only after proof is written.

## Proof Required
- `bundle://proof/SB056/manifest.md`
- command transcript paths
- source assertion transcript
- changed-file hash inventory
- anti-stub scan
- red-team negative proof for critical gates

## Browser Validation Logging
N/A unless browser-visible files are changed unexpectedly; unexpected UI drift must fail and be re-scoped.

## Progression Gate
SB056 may close only after its proof exists and downstream dependencies are checked.

## Suggested Agent Prompt
Implement SB056 for process-runtime-live-openai-verification-host-alpha-v1. Preserve process runtime behavior, keep drivers verification-only, and close proof with source-backed tests instead of report-only claims.
