# SB09 governed proof manifest

Status: BLOCKED for final closure; local implementation/validation complete. Owned requirements R01–R10 and raw notes N01–N06.

- Semantic contract: bundle://proof/SB09/semantic-invariants.md
- Changed source/test/docs/skill hashes: bundle://proof/SB09/changed-files.json
- Raw transcript, screenshot and generated SQL hashes: bundle://proof/SB09/artifacts.json
- Command/result map: bundle://proof/SB09/command-index.md and bundle://proof/SB09/transcripts/validation.txt
- Execution report: bundle://reviews/01-execution-report.md
- Architecture gate: bundle://reviews/csharp-architecture-gate.md
- Scoped independent tool output: bundle://reviews/sb09-codeanalytics.json
- Anti-stub/source assertions: bundle://reviews/sb09-source-audit.log (INV-ARCH)
- Frozen production assembly identity: bundle://proof/SB09/runtime-build.json and bundle://reviews/sb09-binary-identity.log (all nine SHA-256 values match)
- Production behavior artifact matrix: bundle://proof/SB09/semantic-invariants.md
- Desktop inspection: bundle://proof/SB09/ui-review.md
- Verifier scope/remaining review: bundle://proof/SB09/verifier-review.md
- Structural validation: bundle://reviews/execution-structure-validation.txt and bundle://reviews/execution-completed-structure-validation.txt (Pass); semantic final closure remains Blocked

## Source identity

Product branch providers-shared, baseline bb154a0ac4df3b3da092246db30e516521dde7c4; reviewed product head 3fc10d2db7ba7e4e15bc94f50e66f815f31c4219; development 1625b336e4f60ddb64987240c3a3dc485591d20f. Working tree is intentionally uncommitted. The source manifest records SHA-256 before/after for changed files, with null before for new files; proof/raw output is indexed separately to avoid recursive self-hashing.

Dependencies: clean Components 8372c1d55f21b349f8e859470b02eeb4421e96ca, FileTools c95dd07208a6d48724443317cdc6cfe67a13020a. SDK10.0.303 / Release / isolated artifacts/premerge / local source graph. No project/reference/Directory.Build change.

## Behavioral evidence

The single frozen Stable invocation passed 9,424/9,424, exit0, no failures/skips. Initial discovery had 9,369 display entries; seven source-verified deferred MemberData methods account for all 55 extra rows. bundle://reviews/sb09-stable-results.json records per-assembly results, exact theory expansion/source hashes and TRX names; bundle://reviews/sb09-stable-reconciliation.log records the successful counter check.

Final owning Integration 179/179; owning Unit145/145; hot-path Unit110/110; paired ten-case before/after performance workloads; six-case additional plans/concurrency/upgrade selection; 28 real JSON Schema validator cases; documentation197; browser1 with seven inspected screenshots. Exact result identities and hashes are linked above. Counts are overlapping selections and must not be summed as unique tests.

Adversarial baselines are retained: eight SDK failures before post-header abort; quoted credential and timeout failures before repair; two sanitized-boundary timeout failures; naive orphan deletion failed the existing late-retry invariant. Setup/TCP gating/UI navigation failures are labelled iterations and not substituted for failing-first defect proof.

The two migration lanes preserve actual values, canonical file hashes, identities, ownership, quota and supported transfer behavior. No repair migration was needed; reviewed-head SQL delta is empty except UTF-8 BOM. Full and development delta SQL are generated product artifacts, never a second schema source of truth.

## Open gates

- Final canonical localhost:5032 export/SharedInfo support manifest+README and live-byte parity await identified-host and explicit pre-commit capture authority. Existing draft source skills are not installed yet.
- Repository/active skill hash parity is therefore pending for _candoitall-api-shared, candoitall-api-shared-providers, candoitall-api-agents, candoitall-api-llm-chats and candoitall-api-workflows. Preview exists; no active copy is claimed current.
- Original three-application SB07 lifecycle/image authority is unchanged and unexecuted. Two-instance history or isolated visual proof is not a substitute.
- A separate independent execution verifier is required; current source audit is by the implementing agent plus CodeAnalytics.

This manifest deliberately blocks final closure until these facts are resolved. No merge, push, commit or deployment was performed.
