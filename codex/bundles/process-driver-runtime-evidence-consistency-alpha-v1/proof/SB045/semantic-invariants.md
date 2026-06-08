# SB045 Semantic Invariants

- Invariant ID: $invariantId
- Source raw note: Review latest Codex work against real code and keep the Process Core/domain-driver path safe.
- Expected behavior: Runtime host roadmap remains documentation-only.
- Disallowed shallow implementation: A report-only update, non-empty diagnostic assertion, or table-only proof must not close this gate.
- Failing-first test: N/A process non-production compatibility closure; no new behavior changed inside this gate during the current execution pass.
- Passing test: bundle://proof/shared/transcripts/solution-build-no-restore.txt
- Changed source files: bundle://proof/shared/changed-file-hashes.md
- Production assertions: repo://codex/bundles/process-driver-runtime-evidence-consistency-alpha-v1/architecture/04-driver-roadmap-and-release-gates.md
- Red-team negative case: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt
- Downstream dependency check: bundle://reviews/01-execution-report.md
