# SB04 Semantic Invariants

## Invariant SB04_INV_001
- Invariant ID: `SB04_INV_001`
- Source raw note: Prove the representative multi-team/software-delivery process works through production-path automation, not only happy-path status completion.
- Expected behavior: The `software-delivery` template runs through process-mock launch/approval/dispatch, completes the first-pass governance path, skips repair-only path steps after `Quality accepted`, records completed outbox records, maps root execution runs to direct governance steps, verifies seven required AI-agent role assignments, and reads back managed artifact output for scope, architecture, implementation, peer review, QA, security, runtime command writeback, UI screenshot writeback, release approval, rollout, and post-release learning.
- Disallowed shallow implementation: Counting only completed steps, ignoring role assignment coverage, using manual transitions, skipping release-governance artifacts, or accepting artifact records without managed file output.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB04/transcripts/focused-integration.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
- Production assertions: The E2E verifies product owner, delivery manager, solution architect, lead engineer, QA lead, security reviewer, and release manager assignments as `AI agent`, direct governance execution-run mapping, project-scoped run id, finalizer summaries, and managed-file readback for release/governance artifacts.
- Red-team negative case: `bundle://proof/SB04/transcripts/process-core-leakage-scan.txt` proves the software/multi-team representative vocabulary stays out of Process Core and Contracts.

## Invariant SB04_INV_002
- Invariant ID: `SB04_INV_002`
- Source raw note: Decide whether `multi-team-development` should be a stable alias key.
- Expected behavior: No separate `multi-team-development` process key exists. The canonical representative is `software-delivery`, and `ProcessTemplateCatalogInventory` maps `MultiTeamDevelopment` to `software-delivery` with `MappedTemplate` resolution while preserving reverse family readback for both `SoftwareDevelopment` and `MultiTeamDevelopment`.
- Disallowed shallow implementation: Adding a duplicate template definition, introducing fallback template selection, or leaving the multi-team representative ambiguous in catalog readback.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB04/transcripts/focused-integration.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`
- Production assertions: The catalog test asserts `pack.Processes` does not contain `multi-team-development`, `MultiTeamDevelopment` maps to `software-delivery`, the resolution kind is `MappedTemplate`, reverse family mapping includes both software and multi-team families, and the display/summary text preserves multi-team and release-governance semantics.
- Downstream dependency check: SB05 can proceed with `software-delivery` as the unambiguous software/multi-team representative, while business-analysis proof must remain separate and non-software.
