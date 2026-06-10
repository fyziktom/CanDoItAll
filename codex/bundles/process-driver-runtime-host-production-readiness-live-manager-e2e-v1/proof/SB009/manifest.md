# SB009 Proof Manifest

## Objective
- Critical Gate C: no exception-as-control-flow for expected host denials

## Changed File Hashes
- 93FD27194E39439DE4C5DBE476E12AEBABF31814A6F03BD76914FB1FD97E0477 repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
- 8A7B71C23B540F9D2C24FA6EFC757DEA1D38FECC74858EDD93A830302EDD560D repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs
- 3750ABE3E4C75C17CF3B9A582FC087EF24544F9AF776A51C38B955C8DAED1DAC repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs
- C30B5465085FC1D2469F454BBFD1946A73046DF2BBDA305D14A66F31F2940CD4 repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- 144613F324195ED690538A90501BE2F4DE9D9320D617517EB3F388135DD59B2F repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- 60D28F79A74E74B3EB1AA0C1C80C2A8C0AEC4034AC39CAD3AFCCB0BAAD5455FE repo://tests/CanDoItAll.Tests.Components/ProjectStructureProcessAssignmentDialogTests.cs

## Portable References
- repo://codex/bundles/process-driver-runtime-host-production-readiness-live-manager-e2e-v1/subbundles/SB009/README.md
- bundle://proof/SB009/semantic-invariants.md
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs

## Evidence
- Passing: bundle://proof/validation/transcripts/focused-verification-host-and-live-smoke-tests.txt
- Anti-stub audit: bundle://proof/validation/transcripts/source-assertions-and-anti-stub-scan.txt
- Adversarial negative proof: bundle://proof/validation/transcripts/source-assertions-and-anti-stub-scan.txt
- Failing-first: N/A process-level negative proof is covered by committed regression tests and source assertions; no production behavior is intentionally failed outside tests.
- Source proof: bundle://proof/validation/transcripts/source-assertions-and-anti-stub-scan.txt
- Semantic positive proof: bundle://proof/validation/transcripts/focused-verification-host-and-live-smoke-tests.txt

## Semantic Contract
- bundle://proof/SB009/semantic-invariants.md
- Invariant ID: SB009_INV_001

## Review Notes
- Raw note owned: real code and real test outcomes are represented by the cited source, test, and browser transcripts.
- Shipped behavior: production DI uses an explicit durable audit store through the process module, while test-only in-memory audit requires an explicit helper.
- Source proof: source assertions verify no implicit in-memory audit fallback, no parameterless host fallback, no execution-capable verification hook, and no concrete bundle path coupling.
- Test proof: focused integration tests, unit tests, component tests, and large-desktop Playwright proof passed with ExitCode 0 in the cited transcripts.
- Anti-stub audit: source scan rejects stubbed production paths and fake-proof bundle coupling.
