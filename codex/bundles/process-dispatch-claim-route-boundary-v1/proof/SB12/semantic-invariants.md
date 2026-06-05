# SB12 Semantic Invariants

- Invariant ID: `SB12_INV_001`
- Source raw note: RN-001, RN-002, RN-003, and RN-004.
- Expected behavior: Route planning, start-transition request construction, claim guard, and heartbeat boundaries exist while `DispatchAsync` keeps all durable state mutations and runtime side effects.
- Disallowed shallow implementation: A route planner that merely exists but moves EF writes, workflow calls, subprocess service calls, agent execution, transitions, finalizer calls, or logging into helper code.
- Failing-first test: `bundle://proof/SB12/transcripts/sb12-failing-first-head-route-gate.txt` exits `1` against `HEAD` because the pre-refactor source lacks the route planner and SB09-SB11 semantic proof.
- Passing test: `bundle://proof/SB12/transcripts/sb12-architecture-gate-c-tests.txt` and `bundle://proof/SB12/transcripts/sb12-route-claim-integration-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB12/source-assertions/gate-c-route-claim-parity.md`.
- Red-team negative case: `bundle://proof/SB12/transcripts/sb12-failing-first-head-route-gate.txt` and `bundle://proof/SB12/transcripts/sb12-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB13 can build finalizer context factory work on top of route/claim helper boundaries without re-moving side effects.

- Invariant ID: `SB12_INV_002`
- Source raw note: RN-001, RN-002, RN-003, and RN-004.
- Expected behavior: Dispatcher line counts move below entry baselines without creating a new monolith or broadening scope into Process Core, driver APIs, UI, or prohibited viewport proof.
- Disallowed shallow implementation: Passing functional tests while line counts regress, route helpers contain stubs, or proof is satisfied by unrelated browser/mobile artifacts.
- Failing-first test: `bundle://proof/SB12/transcripts/sb12-failing-first-head-route-gate.txt`.
- Passing test: `bundle://proof/SB12/transcripts/sb12-architecture-gate-c-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: `bundle://proof/SB12/source-assertions/gate-c-route-claim-parity.md`.
- Red-team negative case: `bundle://proof/SB12/transcripts/sb12-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB15/SB16 must preserve no-core/no-driver/no-prohibited-proof closure.
