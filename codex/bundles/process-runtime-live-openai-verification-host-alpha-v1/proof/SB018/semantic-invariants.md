# SB018 Semantic Invariants

## SB018-INV-001
- Invariant ID: SB018-INV-001
- Source raw note: Move toward generic runtime host/registry/selector/DI/manager.
- Expected behavior: The registry/selector accepts only declared read-only verification lanes and rejects unsupported or empty lane requests.
- Disallowed shallow implementation: Report-only closure, lane fallback, execution-capable driver hooks, secret logging, bundle-path coupling, or stubbed code.
- Failing-first test: bundle://proof/SB015/transcripts/failing-first-verification-host-tests.txt
- Passing test: bundle://proof/SB015/transcripts/passing-verification-host-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationLaneRegistry.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs, repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs plus bundle://proof/SB057/transcripts/final-red-team-source-scans.txt
- Red-team negative case: bundle://proof/SB057/transcripts/final-red-team-source-scans.txt rejects forbidden host names, execution hooks, stubs, secret leakage, and bundle-path coupling.
- Downstream dependency check: bundle://proof/SB015/transcripts/architecture-boundary-tests-after-host.txt and bundle://proof/SB051/transcripts/build-debug.txt

