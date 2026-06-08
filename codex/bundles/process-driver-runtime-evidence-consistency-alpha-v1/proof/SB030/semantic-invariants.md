# SB030 Semantic Invariants

- Invariant ID: $invariantId
- Source raw note: Review latest Codex work against real code and keep the Process Core/domain-driver path safe.
- Expected behavior: Process runtime evidence adapter maps supplied descriptors without registration or mutation.
- Disallowed shallow implementation: A report-only update, non-empty diagnostic assertion, or table-only proof must not close this gate.
- Failing-first test: bundle://proof/SB030/transcripts/failing-first-runtime-evidence-adapter-before-implementation.txt
- Passing test: bundle://proof/SB030/transcripts/passing-runtime-evidence-adapter-after-architecture-guard.txt
- Changed source files: bundle://proof/shared/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs
- Red-team negative case: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt
- Downstream dependency check: bundle://reviews/01-execution-report.md
