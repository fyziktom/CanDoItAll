# SB006 Semantic Invariants

## SB006-INV-001
- Invariant ID: SB006-INV-001
- Source raw note: Use OpenAI credits for actual test.
- Expected behavior: The guarded live OpenAI smoke runs with bounded token and timeout settings and does not print the secret value.
- Disallowed shallow implementation: Report-only closure, lane fallback, execution-capable driver hooks, secret logging, bundle-path coupling, or stubbed code.
- Failing-first test: N/A process non-production live-smoke gate; no behavior changed by this gate.
- Passing test: bundle://proof/SB006/transcripts/live-openai-specialist-agent-smoke.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationLaneRegistry.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs, repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs, repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs plus bundle://proof/SB057/transcripts/final-red-team-source-scans.txt
- Red-team negative case: bundle://proof/SB057/transcripts/final-red-team-source-scans.txt rejects forbidden host names, execution hooks, stubs, secret leakage, and bundle-path coupling.
- Downstream dependency check: bundle://proof/SB015/transcripts/architecture-boundary-tests-after-host.txt and bundle://proof/SB051/transcripts/build-debug.txt

