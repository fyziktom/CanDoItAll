# SB003 Semantic Invariants

## Invariant SB003-BASELINE-READONLY
- Invariant ID: `SB003-BASELINE-READONLY`
- Source raw note: `Review real code after Codex completion`
- Expected behavior: The bundle execution starts from reopened current source, a green build, green focused process-driver tests, green focused process-adapter integration tests, a green full unit baseline, and targeted source scans.
- Disallowed shallow implementation: Marking P01 complete from prior prose, stale transcripts, or status rows without reopening the gateway, process adapter, Core dependency boundary, and current tests.
- Failing-first test: `N/A - no production behavior changed in this process baseline gate; failing-first proof is represented by source-scan denial and direct-construction inventory.`
- Passing test: bundle://proof/SB001/transcripts/build-no-restore.txt and bundle://proof/SB002/transcripts/full-unit-baseline.txt
- Changed source files: `N/A - no production source changed in SB001-SB003; source baseline hashes are recorded in bundle://proof/SB003/manifest.md.`
- Production assertions: repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs remains explicit and typed; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs still exposes the downstream direct-construction gap; repo://src/CanDoItAll.Processes.Core remains driver-free.
- Red-team negative case: bundle://proof/SB001/transcripts/targeted-source-scans-baseline.txt inventories direct verifier construction in the process adapter target, preventing report-only closure from hiding later work.
- Downstream dependency check: P02 may start only because baseline build, full unit, focused unit, focused integration, Core reverse-dependency scan, anti-stub scan, and UI/media drift scan are captured under proof/SB001-SB003.
