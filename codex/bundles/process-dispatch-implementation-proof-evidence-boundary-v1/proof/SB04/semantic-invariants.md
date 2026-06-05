# SB04 Semantic Invariants

- Invariant ID: SB04-IPBOUNDARY-001
- Source raw note: Continue smaller dispatcher isolation steps, preserve behavior, do not create Process Core or production driver APIs, and avoid UI proof drift.
- Expected behavior: Module-local architecture guard rejects Process Core, production driver APIs, helper stubs, and UI proof drift while keeping existing wrappers callable.
- Disallowed shallow implementation: A shallow extraction that adds Process Core, driver API names, TODO stubs, or UI artifacts.
- Failing-first test: N/A - process non-production/no behavior exemption for behavior-preserving refactor; adversarial cases are covered by focused negative assertions and scans.
- Passing test: Architecture guard unit test and boundary scans passes in bundle://proof/SB28/transcripts/build-solution.txt, bundle://proof/SB28/transcripts/unit-architecture-guard.txt, bundle://proof/SB28/transcripts/source-boundary-scan.txt, bundle://proof/SB28/transcripts/anti-stub-scan.txt, bundle://proof/SB28/transcripts/no-ui-proof-drift-scan.txt.
- Changed source files: 97854efe71d79a77c243dd0785c92c90a5212028bbc514e46fc3ff4236d1d832  repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs; 59b5a7d22ee83752d1972afcede951c1c1fa13fc3daa19c49778a6ab503738db  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs.
- Production assertions: Existing wrappers delegate to module-local helpers and no production API surface is added.
- Red-team negative case: A shallow extraction that adds Process Core, driver API names, TODO stubs, or UI artifacts.
- Downstream dependency check: SB05-SB08 allowed to continue after guard passed.
