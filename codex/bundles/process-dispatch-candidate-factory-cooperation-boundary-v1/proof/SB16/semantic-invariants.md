# SB16 Semantic Invariants

- Invariant ID: SB16-INV-001
- Source raw note: Require focused tests, source scans, line counts, and full solution build, not compile-only proof.
- Expected behavior: Runtime/service refactor builds successfully, focused unit/integration tests pass, line counts are recorded, constructor ownership is scanned, and helper anti-stub/source guardrails pass.
- Disallowed shallow implementation: Compile-only closure without proving route parity, side-effect boundaries, line counts, or anti-stub scans.
- Failing-first test: N/A process/non-production exemption: SB16 is a runtime smoke/proof gate and did not introduce separate behavior beyond earlier route movement.
- Passing test: bundle://proof/SB16/transcripts/sb16-full-solution-build.txt; bundle://proof/SB16/transcripts/sb16-line-counts-and-source-scans.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Cooperation.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCooperationMetadataResolver.cs
- Production assertions: Full solution build exits 0 and source scans show only the factory owns DispatchCandidate construction.
- Red-team negative case: SB16 scans reject process-core/driver drift, hidden side effects, helper stubs, and prohibited proof paths.
- Downstream dependency check: SB17 final closure uses SB16 build and scan proof.
