# SB05 Semantic Invariants

- Invariant ID: `SB05_INV_001`
- Source raw note: RN-001 and RN-003.
- Expected behavior: Route snapshot facts expose trigger, run/step status, step kind, agent automation, recovery execution run, and current-attempt context without moving side effects.
- Disallowed shallow implementation: Adding an unused record that does not feed dispatcher decisions or a helper that performs EF/workflow/subprocess/agent side effects.
- Failing-first test: N/A - non-critical production helper foundation; behavior was preserved by wrapper tests and build proof.
- Passing test: `bundle://proof/SB05/transcripts/sb05-route-snapshot-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`.
- Production assertions: `bundle://proof/SB05/source-assertions/route-snapshot-foundation.md`.
- Red-team negative case: `bundle://proof/SB05/transcripts/sb05-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB06/SB11 can use route facts without depending on side-effect movement.

- Invariant ID: `SB05_INV_002`
- Source raw note: RN-001.
- Expected behavior: Existing run/step eligibility wrappers preserve failed-run reopened-step behavior while delegating to the local helper.
- Disallowed shallow implementation: Changing step-status dispatchability to implicitly include terminal run filtering and breaking the existing two-stage selection contract.
- Failing-first test: N/A - non-critical production helper foundation; an early local run caught and corrected the terminal-run expectation without production behavior change.
- Passing test: `bundle://proof/SB05/transcripts/sb05-route-snapshot-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`.
- Production assertions: `bundle://proof/SB05/source-assertions/route-snapshot-foundation.md`.
- Red-team negative case: `bundle://proof/SB05/transcripts/sb05-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB06/SB08 concurrency work must preserve wrapper entry points and the two-stage run/step eligibility contract.
