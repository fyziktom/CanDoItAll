# SB05 Semantic Invariants

- Invariant ID: SB05-INV-001
- Source raw note: Workflow runtime state, records, artifacts, and policy must align with durable execution expectations.
- Expected behavior: Runtime dispatch refuses in-process execution when policy requires durable production execution without previews, records executor progress with stable node ids, and preserves artifact records.
- Disallowed shallow implementation: Returning generic runtime failure text, omitting node ids, or allowing preview execution despite durable-only policy.
- Failing-first test: N/A - process hardening tightened runtime policy and record projection with targeted negative tests.
- Passing test: Runtime event/artifact tests and runtime evidence integration passed.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`, `repo://tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`.
- Production assertions: Runtime manager checks durable policy before backend dispatch, and the MAF backend records progress plus artifacts from configured executor output.
- Red-team negative case: A durable-only workflow requested through the in-process backend fails before dispatch.
- Downstream dependency check: Integration runtime evidence test proves persisted evidence projection remains redacted and consumable.
