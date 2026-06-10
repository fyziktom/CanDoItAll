# SB027 — Gate I manager diagnostics closure

## Status
- Status: Completed

## Objective
Execute the gate i manager diagnostics closure slice for **P09: Manager command UI/API and evidence projection**.

## Covered Inputs
- Raw request for real-code review, live OpenAI proof, and progress toward generic Process Core with domain drivers.
- Normalized requirements in `bundle://requirements/01-normalized-requirements.md`.

## Prerequisites
- Previous subbundle gate must pass.
- For critical subbundles, all upstream source assertions and transcripts must exist before implementation continues.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.BusinessAnalysis`
- `repo://src/CanDoItAll.Processes.Drivers.ObservationAggregation`
- `repo://src/CanDoItAll.Processes.Drivers.OfficeEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway`
- `repo://tests/CanDoItAll.Tests.Unit`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Playwright`
- `bundle://analysis/01-current-state-review.md`
- `bundle://architecture/01-target-architecture.md`

## Deliverables
- Source changes only if needed for this slice.
- Tests proving the behavior and rejecting the shallow/unsafe alternative.
- Proof artifacts under `bundle://proof/SB027/`.

## Dependency Impact
- This subbundle belongs to the phase named in its objective and gates downstream phases through `bundle://plan/01-phase-plan.md`.
This subbundle belongs to P09. Its output feeds downstream phases in `bundle://plan/01-phase-plan.md`. If this proof is wrong, later host/manager/runtime conclusions are untrustworthy.

## Validation Depth
- Execute the focused proof listed in this subbundle; critical gate subbundles also require semantic adequacy manifest and invariant proof.
Critical semantic adequacy gate with artifact-backed manifest, command transcripts, changed-file hashes, source assertions, anti-stub audit, red-team negative proof, and downstream dependency check.

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
- `bundle://proof/SB027/manifest.md`
- command transcript paths
- source assertion transcript
- changed-file hash inventory
- anti-stub scan
- red-team negative proof for critical gates

## Production Behavior Artifact Matrix Required
If this subbundle creates or changes a production signal, state, record, event, host surface, audit record, or manager command, the proof manifest and semantic invariant contract must include producer, consumer, lifecycle, negative-test, and source citations.

## Browser Validation Logging
- N/A unless browser-visible or host-visible files change unexpectedly; unexpected UI drift must fail and be re-scoped.
N/A unless browser-visible files are changed unexpectedly; unexpected UI drift must fail and be re-scoped.

## Progression Gate
- The subbundle may close only after its proof exists, entry and closure gate rows are updated, and downstream dependencies are checked.
SB027 may close only after its proof exists and downstream dependencies are checked.

## Suggested Agent Prompt
Implement SB027 for process-runtime-live-openai-verification-host-alpha-v1. Preserve process runtime behavior, keep drivers verification-only, and close proof with source-backed tests instead of report-only claims.


