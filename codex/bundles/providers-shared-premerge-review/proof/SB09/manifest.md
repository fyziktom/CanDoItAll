# SB09 governed proof manifest

Status: BLOCKED for final closure only on original three-application proof and independent implementation review. Owned requirements R01–R10 and raw notes N01–N07. Finishing live acceptance and SB08 are complete. Current baseline aadd953150e7f659e4060ced6505621c705ea61f plus UI repair: bundle://proof/SB09/finishing/manifest.json and bundle://reviews/05-finishing-acceptance.md. Earlier hashes below are preserved historical checkpoint evidence, not silently regenerated under a different baseline.

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

## Original source identity

Product branch providers-shared, baseline bb154a0ac4df3b3da092246db30e516521dde7c4; reviewed product head 3fc10d2db7ba7e4e15bc94f50e66f815f31c4219; development 1625b336e4f60ddb64987240c3a3dc485591d20f. Working tree is intentionally uncommitted. The source manifest records SHA-256 before/after for changed files, with null before for new files; proof/raw output is indexed separately to avoid recursive self-hashing.

Dependencies: clean Components 8372c1d55f21b349f8e859470b02eeb4421e96ca, FileTools c95dd07208a6d48724443317cdc6cfe67a13020a. SDK10.0.303 / Release / isolated artifacts/premerge / local source graph. No project/reference/Directory.Build change.

## Behavioral evidence

The single frozen Stable invocation passed 9,424/9,424, exit0, no failures/skips. Initial discovery had 9,369 display entries; seven source-verified deferred MemberData methods account for all 55 extra rows. bundle://reviews/sb09-stable-results.json records per-assembly results, exact theory expansion/source hashes and TRX names; bundle://reviews/sb09-stable-reconciliation.log records the successful counter check.

Final owning Integration 179/179; owning Unit145/145; hot-path Unit110/110; paired ten-case before/after performance workloads; six-case additional plans/concurrency/upgrade selection; 28 real JSON Schema validator cases; documentation197; browser1 with seven inspected screenshots. Exact result identities and hashes are linked above. Counts are overlapping selections and must not be summed as unique tests.

Adversarial baselines are retained: eight SDK failures before post-header abort; quoted credential and timeout failures before repair; two sanitized-boundary timeout failures; naive orphan deletion failed the existing late-retry invariant. Setup/TCP gating/UI navigation failures are labelled iterations and not substituted for failing-first defect proof.

The two migration lanes preserve actual values, canonical file hashes, identities, ownership, quota and supported transfer behavior. No repair migration was needed; reviewed-head SQL delta is empty except UTF-8 BOM. Full and development delta SQL are generated product artifacts, never a second schema source of truth.

## Open gates

- Closed2026-08-31: canonical localhost:5032 capture and SharedInfo snapshot/manifest/README pass identified-host byte parity. SHA-25614FE4C527863FF84948ED96D3D7A3B16FD46D3E315E673E96EEF3911C3D2A52B; bundle://proof/SB09/finishing/api-export.json.
- Closed2026-08-31: all11 source/active files across the five selected packages match after installation; bundle://proof/SB09/finishing/installed-skill-hashes.json. Four skill validators and both SharedInfo validators pass.
- Original three-application SB07 lifecycle/image authority is unchanged and unexecuted. Two-instance history or isolated visual proof is not a substitute.
- A separate independent execution verifier is required; current source audit is by the implementing agent plus CodeAnalytics.

This manifest deliberately blocks overall merge closure on the two remaining gates. The finishing request authorized adoption on5032/5210/5214 and bounded live inference; these actions passed and the apps remain running. No merge, push or commit was performed.
