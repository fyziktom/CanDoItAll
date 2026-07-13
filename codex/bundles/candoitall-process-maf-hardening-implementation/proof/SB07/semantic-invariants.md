# Semantic Invariants - SB07

## INV-SB07-01

- Invariant ID: `INV-SB07-01`
- Source raw note: F08 requires exact runtime tool preflight before agent execution.
- Expected behavior: missing required runtime tools block deterministically before `ExecuteRunAsync`.
- Disallowed shallow implementation: discovering missing tools only after an LLM run fails.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeToolPreflightService.cs`, `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`, `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`.
- Production assertions: adapter invokes injected preflight service with exact process-step context and returns `process.adapter.runtime_tool_preflight_failed`.
- Red-team negative case: missing `workspace_dotnet_build` does not call the agent workspace execution service.
- Downstream dependency check: SB08 and SB09 rely on deterministic pre-agent blockers.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Runtime tool preflight result | preflight service source/test | adapter diagnostics/operator packet | before-agent-execution lifecycle | missing mandatory tool does not call agent |
