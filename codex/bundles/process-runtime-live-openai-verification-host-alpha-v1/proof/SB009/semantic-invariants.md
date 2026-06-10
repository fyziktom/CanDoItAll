# SB009 Semantic Invariants

## SB009-INV-001
- Invariant ID: SB009-INV-001
- Source raw note: Use OpenAI credits for actual test.
- Expected behavior: The live route proof is represented by the guarded specialist-agent smoke and redaction scan.
- Disallowed shallow implementation: Report-only closure, lane fallback, execution-capable driver hooks, secret logging, bundle-path coupling, or stubbed code.
- Failing-first test: N/A process non-production live-route gate; no behavior changed by this gate.
- Passing test: bundle://proof/SB006/transcripts/live-openai-specialist-agent-smoke.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationLaneRegistry.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs, repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs plus bundle://proof/SB057/transcripts/final-red-team-source-scans.txt
- Red-team negative case: bundle://proof/SB057/transcripts/final-red-team-source-scans.txt rejects forbidden host names, execution hooks, stubs, secret leakage, and bundle-path coupling.
- Downstream dependency check: bundle://proof/SB015/transcripts/architecture-boundary-tests-after-host.txt and bundle://proof/SB051/transcripts/build-debug.txt

