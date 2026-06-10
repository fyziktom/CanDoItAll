# SB030 Semantic Invariants

- Invariant ID: SB030_INV_001
- Source raw note: The architect required real-code verification, real tests, generic runtime boundaries, durable audit proof, live-manager/browser evidence, and fake-proof rejection.
- Expected behavior: Critical Gate J: scenario matrix proves generic runtime and no domain leakage into Core is satisfied by source-backed implementation or by explicit source-backed verification for already-existing behavior.
- Disallowed shallow implementation: Do not satisfy this gate with report-only text, hidden in-memory fallback, fallback lane selection, execution-capable driver authority, or skipped live/browser proof represented as success.
- Failing-first test: N/A process-level negative proof is represented by committed regression tests and source assertions; no production behavior is intentionally failed outside the test harness.
- Passing test: bundle://proof/validation/transcripts/playwright-process-run-detail-large-screen.txt records ExitCode 0 for this gate's positive proof path.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs; repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs; repo://tests/CanDoItAll.Tests.Components/ProjectStructureProcessAssignmentDialogTests.cs.
- Production assertions: Verification remains read-only, audit persistence is explicit, live smoke settings are explicit and bounded, and large-desktop UI readback uses the existing Playwright process route.
- Red-team negative case: bundle://proof/validation/transcripts/source-assertions-and-anti-stub-scan.txt rejects hidden authority and stubbed production paths; bundle://proof/validation/transcripts/semantic-invariant-catalog-and-ui-artifact-scan.txt rejects live smoke fallback defaults and missing browser artifacts.
- Downstream dependency check: bundle://reviews/01-execution-report.md records this subbundle closure and cites bundle://proof/SB030/manifest.md.
