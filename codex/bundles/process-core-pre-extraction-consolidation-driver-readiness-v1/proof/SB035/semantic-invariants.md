# SB035 Semantic Invariants

## Invariants

- Invariant ID: `SB035-INV-001`
- Source raw note: `Review whether a narrow Core proposal is now justified; list exact blockers if not.`
- Expected behavior: Final red-team review approves only a narrow future Core proposal for pure read models and deterministic rules, while listing exact blockers for broad extraction and keeping driver APIs documentation-only.
- Disallowed shallow implementation: Approving broad Core movement, failing to list side-effect blockers, omitting line-count evidence, or allowing production driver APIs.
- Failing-first test: `N/A - review-only closure; no production behavior change was intended.`
- Passing test: `bundle://proof/SB035/transcripts/red-team-source-assertions.txt`
- Changed source files: `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/reviews/02-final-red-team-review.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/reviews/01-execution-report.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/README.md`
- Production assertions: `bundle://proof/SB035/transcripts/red-team-source-assertions.txt`
- Red-team negative case: Moving a high-line-count dispatcher partial wholesale, approving EF/workspace/storage/AgentFramework/finalizer/claim behavior for Core, or adding a production driver API fails SB035 proof.
- Downstream dependency check: `SB036` may complete final closure because the red-team review gives a concrete narrow-Core next decision and blocks broad/driver runtime scope.

## Raw Note Closure

- Do not rush Process Core: `Solved for SB035 by approving only a narrow future proposal and blocking broad extraction.`
- Move closer to Process Core and drivers safely: `Solved for SB035 with exact candidates and blocker list.`
- No production driver API: `Solved for SB035 by preserving documentation-only driver readiness.`
