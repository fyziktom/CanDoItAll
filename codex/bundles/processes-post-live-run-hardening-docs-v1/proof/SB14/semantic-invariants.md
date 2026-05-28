# SB14 Semantic Invariants

## Invariants

- Invariant ID: `SB14-INV-001`
- Source raw note: RN14 - Protect generic Processes behavior beyond software delivery.
- Expected behavior: Baseline scenarios must cover nonsoftware and agent-improvement processes with typed operation contracts, branch selections, artifact records, and recovery exercises that match the projected process definitions.
- Disallowed shallow implementation: prompt-only, docs-only for runtime behavior, source-only proof for runtime behavior, UI-only hiding of errors, or hardcoded project/run/Tetris/Blazor special cases.
- Failing-first test: bundle://proof/SB14/transcripts/failing-first.txt records that `baseline-agent-training-and-improvement` was absent from the seed catalog and governance matrix before SB14.
- Passing test: bundle://proof/SB14/transcripts/passing.txt records 12 passing integration tests across `ProcessTemplateGovernanceTests` and `BusinessPlanProcessPostgresIntegrationTests`.
- Changed source files: repo://Templates/Processes/seed-catalog/baseline-scenarios.json; repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs.
- Production assertions: The new agent-improvement scenario uses `ai-assisted-change-delivery`, selects real branch outcomes (`delegate`, `rework`), records agent trace/evaluation/safety artifacts, and validates recovery options for `RuntimeEvidence` and `PolicyDenied` blocks.
- Red-team negative case: A future template pack cannot claim generic readiness if it only proves software/Blazor scenarios or drops agent-improvement contract/recovery metadata.
- Downstream dependency check: SB18 can use the baseline matrix to reject software-only assumptions during final release-readiness red-team.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| `baseline-agent-training-and-improvement` seed scenario | `Templates/Processes/seed-catalog/baseline-scenarios.json`. | Development seed service, API/MAF baseline scenario readers, governance tests, SB18 red-team. | Loaded by `ProcessTemplatePackLoader` and validated by governance tests. | Pre-change source absence in `bundle://proof/SB14/transcripts/failing-first.txt`. |
| Typed contract/recovery exercises | Scenario JSON plus `ProcessTemplateGovernanceTests`. | Template maintainers and CI. | Exercises selected branch outcomes, exact allowed-operation contracts, and block recovery options. | Passing governance test in `bundle://proof/SB14/transcripts/passing.txt`. |
| Business-plan PostgreSQL runtime proof | `BusinessPlanProcessPostgresIntegrationTests`. | Runtime maintainers and SB18 generic-process red-team. | Proves a non-code process can project, import, run, persist artifacts, and complete on PostgreSQL. | Passing no-build integration slice in `bundle://proof/SB14/transcripts/passing.txt`. |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB14/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB14/transcripts/passing.txt.
- Source assertions: bundle://proof/SB14/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB14/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB14/transcripts/changed-file-hashes.txt.
