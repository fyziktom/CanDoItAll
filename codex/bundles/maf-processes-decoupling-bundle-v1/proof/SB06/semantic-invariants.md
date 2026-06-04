# SB06 Semantic Invariants

## Invariant `SB06-INV-001`

- Invariant ID: SB06-INV-001

- Source raw note: MAF and Processes must be decoupled without simplifying or omitting behavior.
- Expected behavior: The regression suite proves exact process tool names, read-vs-mutation approval behavior, provider registration behavior, no-provider MAF behavior, and direct dependency guardrails together.
- Disallowed shallow implementation: Relying on counts only, weakening existing assertions, testing only policy but not runtime registration, or testing only runtime but not `ToolContractCatalog` / `ToolCapabilityRegistry`.
- Failing-first test and transcript: `bundle://proof/SB06/transcripts/agent-tool-invocation-policy-tests.txt` fails if any expected process tool is absent from the known-tool catalog or capability registry; `bundle://proof/SB06/transcripts/agent-runtime-tool-provider-tests.txt` fails if MAF reintroduces direct Processes references or leaks process tools with zero providers.
- Passing test and transcript: Required `AgentRuntimeToolProvider`, `AgentToolInvocationPolicy`, and `AgentFrameworkExecutionCapabilityFiltering` transcripts pass; `process-agent-runtime-tool-provider-tests.txt` and `solution-build.txt` also pass.
- Changed source files and hashes: `bundle://proof/SB06/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB06/source-assertions/process-tool-regression-source-assertion.txt` and `sb06-test-source-audit.txt`.
- Red-team negative case: Missing tools, missing policy/capability registration, direct MAF Processes reference, or process tool attachment without a provider would now fail targeted tests.
- Downstream dependency check: SB07 can start because SB06 proves provider registration and policy/parity guardrails before runtime smoke.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB06 adds regression tests only; it introduces no persisted production signal, state, record, or event. | N/A | N/A | N/A |
