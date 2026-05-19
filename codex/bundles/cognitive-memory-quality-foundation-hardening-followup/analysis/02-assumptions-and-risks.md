# Assumptions And Risks

## Working Assumptions

- The phase-one interfaces are useful enough to keep unless tests prove the abstraction is wrong.
- The system should prefer explicit errors and failed run records over silent fallback behavior.
- Idempotency must be durable across process restarts and repeated explicit dream runs, not only same-request replay.
- A deterministic synthesis implementation is acceptable if it produces a real merged brief, keeps source refs per statement, and can later be replaced by a semantic provider without changing the durable contract.
- UI proof remains out of scope unless an implementation subbundle adds UI or host-visible behavior.

## Critical Path Risks

- If cluster IDs are not stable across repeat planning, dream runs and aggregate candidates can reference non-existent clusters.
- If dream-run failures leave `Running` rows, operators and automation cannot distinguish in-progress work from failed work.
- If unsupported modes use default broad behavior, the system can claim mode-specific dreaming while executing a generic pass.
- If aggregate text includes restricted or redacted content, later recall synthesis can leak it even when references are hidden by default.
- If recall synthesis is only formatting selected memory lines, it will not satisfy the original goal of useful synthesized memory output.
- If refactoring happens before tests expose the current defects, the agent may accidentally preserve broken behavior.

## Validation Risks

- Happy-path count assertions can pass while idempotency, FK integrity, and failure cleanup remain broken.
- In-memory SQLite and `EnsureCreated` tests may not catch all migration and provider-specific behavior; migration project builds are still required.
- Tests that assert only "some candidate exists" do not prove correct cluster selection, mode policy, or provenance quality.
- Access/redaction tests must check aggregate canonical text, synthesized brief text, and reference expansion results separately.
- Full CognitiveMemory test filters may be slower, but the follow-up changes cross recall, consolidation, persistence, and review surfaces.

## Reopen Triggers

- A hidden or new test proves repeat dream runs fail because existing cluster rows are skipped.
- Any subbundle finds source-item clustering is required by public contracts and cannot be safely postponed.
- A dream mode cannot be implemented honestly without new provider infrastructure; the bundle must then require explicit unsupported-mode behavior instead of default selection.
- Any policy test shows restricted source text in aggregate text, synthesized brief, or included reference payload.
- Any refactor changes public DTOs, migrations, or existing tests outside the planned scope.
