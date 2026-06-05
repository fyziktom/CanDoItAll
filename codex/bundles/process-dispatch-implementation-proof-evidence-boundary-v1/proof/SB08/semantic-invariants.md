# SB08 Semantic Invariants

- Invariant ID: SB08-IPBOUNDARY-001
- Source raw note: Continue smaller dispatcher isolation steps, preserve behavior, do not create Process Core or production driver APIs, and avoid UI proof drift.
- Expected behavior: Contract text, runnable signals, explicit test requests, .NET detection, JavaScript detection, and negation rules stay behind wrappers with the same outcomes.
- Disallowed shallow implementation: A helper that treats negated JavaScript or .NET contract text as an affirmative stack requirement.
- Failing-first test: N/A - process non-production/no behavior exemption for behavior-preserving refactor; adversarial cases are covered by focused negative assertions and scans.
- Passing test: Contract and stack focused integration filter passes in bundle://proof/SB28/transcripts/build-solution.txt, bundle://proof/SB28/transcripts/unit-architecture-guard.txt, bundle://proof/SB28/transcripts/source-boundary-scan.txt, bundle://proof/SB28/transcripts/anti-stub-scan.txt, bundle://proof/SB28/transcripts/no-ui-proof-drift-scan.txt, bundle://proof/SB28/transcripts/integration-contract-stack.txt.
- Changed source files: ca6564506b0722c8eac23303b9ca159bca42151081e2291fee0cb49a56b232de  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessImplementationStackRules.cs; 59b5a7d22ee83752d1972afcede951c1c1fa13fc3daa19c49778a6ab503738db  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs; 97854efe71d79a77c243dd0785c92c90a5212028bbc514e46fc3ff4236d1d832  repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: Existing wrappers delegate to module-local helpers and no production API surface is added.
- Red-team negative case: A helper that treats negated JavaScript or .NET contract text as an affirmative stack requirement.
- Downstream dependency check: SB09-SB13 allowed to continue after stack parity passed.
