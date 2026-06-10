# SB020 — No fallback/discovery/reflective selection tests

## Status
- Status: `Completed`

## Objective
No fallback/discovery/reflective selection tests as part of P07: Registry and selector hardening.

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
- Proof transcript paths under `proof/SB020/transcripts/`.
- Updated execution report row.
- Noncritical proof may roll up into the next critical gate, but must still have source-backed transcript references.

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
- Roll-up proof into nearest critical gate.

## Proof Captured
- `bundle://proof/SB020/transcripts/no-fallback-discovery-reflection-source-assertions.txt`
- `bundle://proof/SB020/transcripts/selector-hardening-focused-tests.txt`
- `bundle://proof/SB021/manifest.md`

## Browser Validation Logging
- N/A unless UI route, manager diagnostics UI, or live process-run UI proof is changed. Large desktop only.

## Progression Gate
- Standard: downstream phase may continue only after source-backed proof exists or a critical gate owns it.

## Suggested Agent Prompt
Implement SB020 for `process-driver-verification-host-beta-live-process-proof-v1`. Re-read current source first, avoid report-only closure, preserve no-mutation and Core genericity guarantees, and capture proof before updating status.
