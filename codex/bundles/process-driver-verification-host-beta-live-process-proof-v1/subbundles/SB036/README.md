# SB036 — Critical Gate L process runtime matrix

## Status
- Status: `Completed`

## Objective
Critical Gate L process runtime matrix as part of P12: Process runtime regression matrix.

## Covered Inputs
- Raw request to review real code and test outcome.
- Current source artifacts listed in `inputs/source-artifacts-reviewed.md`.
- Requirements in `requirements/01-normalized-requirements.md`.

## Prerequisites
- All previous subbundles in numeric order must have passed.
- For critical gates, all upstream proof manifests must exist and be source-backed.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
- repo://tests/CanDoItAll.Tests.Integration
- repo://tests/CanDoItAll.Tests.Unit
- bundle://analysis/01-current-state.md
- bundle://architecture/01-target-architecture.md

## Deliverables
- Source or test changes needed for this subbundle.
- Proof transcript paths under `proof/SB036/transcripts/`.
- Updated execution report row.
- Critical proof manifest `proof/SB036/manifest.md` and semantic invariants `proof/SB036/semantic-invariants.md`.

## Dependency Impact
- Downstream phases must not proceed if this subbundle weakens process runtime launch, verification host no-mutation guarantees, Core dependency cleanliness, or live OpenAI policy.

## Validation Depth
- Build/focused tests when source changes.
- Source scans for forbidden runtime/mutation/Core/UI/bundle-path drift.
- Live OpenAI proof only in the explicit live phases.

## Implementation Steps
1. Re-read exact source references before editing.
2. Make the smallest production changes that satisfy the objective.
3. Add or update tests before relying on reports.
4. Capture command transcripts and source assertions.
5. Update `reviews/01-execution-report.md` only after proof exists.

## Scope Exceptions
- Execution-capable process drivers remain out of scope unless a future approved subbundle explicitly changes the policy. This bundle does not approve them.

## Do Not Do
- Do not add generic object payload dispatch.
- Do not add fallback selector behavior.
- Do not log OpenAI API keys or raw secrets.
- Do not mutate process state from driver host paths.
- Do not add Process Core references to drivers/modules/infrastructure.
- Do not claim live-provider functionality from a skipped test.

## Acceptance Checklist
- [x] Objective is implemented or explicitly rejected with source-backed reason.
- [x] Existing runtime behavior is preserved.
- [x] No prohibited dependency or mutation surface is introduced.
- [x] Proof transcripts are captured.
- [x] Execution report row is updated.

## Proof Required
- Source assertions.
- Focused tests where applicable.
- Source scans for forbidden drift.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.
- Artifact-backed manifest with changed-file hashes and production behavior artifact matrix.

## Browser Validation Logging
- N/A unless UI route, manager diagnostics UI, or live process-run UI proof is changed. Large desktop only.

## Progression Gate
- Critical: downstream phases are blocked until semantic adequacy proof passes.

## Suggested Agent Prompt
Implement SB036 for `process-driver-verification-host-beta-live-process-proof-v1`. Re-read current source first, avoid report-only closure, preserve no-mutation and Core genericity guarantees, and capture proof before updating status.
