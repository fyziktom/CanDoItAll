# SB003 Semantic Invariants

- Invariant ID: SB003-INV-001
- Source raw note: REQ coverage for SB003 - Gate A source-backed baseline is closed by live source, tests, source scans, and bundle proof artifacts.
- Expected behavior: The process driver gateway and process-module adapters remain explicit, typed, supplied-evidence-only, and read-only.
- Disallowed shallow implementation: Report-only completion, skipped-test closure, generic lane dispatch, runtime host wiring, persistence, external calls, or mutation hooks are not accepted.
- Failing-first test: bundle://proof/transcripts/failing-first-summary.txt
- Passing test: bundle://proof/transcripts/passing-validation-summary.txt
- Changed source files: bundle://proof/changed-file-hashes.md
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs show typed read-only calls only.
- Red-team negative case: bundle://proof/transcripts/anti-stub-source-scan-summary.txt proves no stubs, no skip ledger, and no forbidden runtime hooks.
- Downstream dependency check: bundle://proof/source-scans/core-reverse-driver-dependency-scan.txt and bundle://proof/source-scans/driver-packages-forbidden-dependencies-scan.txt prove Process Core and driver package boundaries remain clean.
