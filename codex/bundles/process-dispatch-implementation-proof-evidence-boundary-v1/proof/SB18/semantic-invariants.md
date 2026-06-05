# SB18 Semantic Invariants

- Invariant ID: SB18-IPBOUNDARY-001
- Source raw note: Continue smaller dispatcher isolation steps, preserve behavior, do not create Process Core or production driver APIs, and avoid UI proof drift.
- Expected behavior: Runnable host proof and .NET host path discovery stay module-local and keep JavaScript bypass, .NET startup, and mutation ordering behavior unchanged.
- Disallowed shallow implementation: A helper that requires .NET host proof for JavaScript contracts or accepts completed .NET web work without startup proof.
- Failing-first test: N/A - process non-production/no behavior exemption for behavior-preserving refactor; adversarial cases are covered by focused negative assertions and scans.
- Passing test: Runnable .NET focused integration filter passes in bundle://proof/SB28/transcripts/build-solution.txt, bundle://proof/SB28/transcripts/unit-architecture-guard.txt, bundle://proof/SB28/transcripts/source-boundary-scan.txt, bundle://proof/SB28/transcripts/anti-stub-scan.txt, bundle://proof/SB28/transcripts/no-ui-proof-drift-scan.txt, bundle://proof/SB28/transcripts/integration-runnable-dotnet.txt.
- Changed source files: 57472308ec1a353b3343e5f6b001fa7b5eaaa506fd463a88de61b2677095c72b  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDotNetHostEvidenceRules.cs; 59b5a7d22ee83752d1972afcede951c1c1fa13fc3daa19c49778a6ab503738db  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs; 97854efe71d79a77c243dd0785c92c90a5212028bbc514e46fc3ff4236d1d832  repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: Existing wrappers delegate to module-local helpers and no production API surface is added.
- Red-team negative case: A helper that requires .NET host proof for JavaScript contracts or accepts completed .NET web work without startup proof.
- Downstream dependency check: SB19-SB23 allowed to continue after runnable/.NET parity passed.
