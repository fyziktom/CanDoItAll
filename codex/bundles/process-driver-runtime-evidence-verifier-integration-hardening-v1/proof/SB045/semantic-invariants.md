# SB045 Semantic Invariants

- Invariant ID: SB045_INV_001
- Source raw note: Close with build, tests, source scans, validators, and handoff proof
- Expected behavior: Solution build, focused unit/integration suites, filtered broad unit suite, source audit, and validator proof are captured; full unit failure is documented as unrelated stale fixtures plus one host timing test.
- Disallowed shallow implementation: Returning a success flag, status row, or template text without enforcing the read-only evidence boundary and without command/source proof is not enough.
- Failing-first test: bundle://proof/SB045/transcripts/full-unit-tests-no-restore.txt
- Passing test: bundle://proof/SB045/transcripts/unit-tests-excluding-known-unrelated-stale-fixtures.txt
- Changed source files: bundle://proof/SB045/changed-file-hashes.md
- Production assertions: repo://codex/bundles/process-driver-runtime-evidence-verifier-integration-hardening-v1/reviews/01-execution-report.md; source audit bundle://proof/SB045/transcripts/source-boundary-and-anti-stub-audit-after-uri-policy.txt
- Red-team negative case: bundle://proof/SB045/transcripts/full-unit-tests-no-restore.txt; source audit confirms no runtime, DI, file/network, UI/media, TODO, or NotImplemented drift.
- Downstream dependency check: P15 closes with completed validator transcript after proof sync.

## Notes
- Final smoke and red-team closure closed with repo:// source references and bundle:// proof transcripts.
