# SB051 Semantic Invariants

- Invariant ID: $invariantId
- Source raw note: Review latest Codex work against real code and keep the Process Core/domain-driver path safe.
- Expected behavior: Broad smoke and fake-proof audit evidence is present.
- Disallowed shallow implementation: A report-only update, non-empty diagnostic assertion, or table-only proof must not close this gate.
- Failing-first test: N/A process non-production compatibility closure; no new behavior changed inside this gate during the current execution pass.
- Passing test: bundle://proof/shared/transcripts/unit-tests-excluding-stale-architecture-fixtures.txt
- Changed source files: bundle://proof/shared/changed-file-hashes.md
- Production assertions: repo://codex/bundles/process-driver-runtime-evidence-consistency-alpha-v1/reviews/01-execution-report.md
- Red-team negative case: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt
- Downstream dependency check: bundle://reviews/01-execution-report.md
