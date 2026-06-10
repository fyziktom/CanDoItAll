# SB009 Semantic Invariants

## Gate C Invariants
- A skipped live test is not live provider proof.
- Workspace specialist-agent smoke is not Process module process-run proof.
- Process-run proof must create or load a `ProcessRun` through Process module APIs.
- Process-run proof must dispatch through `IProcessRunAutomationDispatchService`; direct calls to workspace chat APIs are insufficient for Gate C.
- Process-run proof must assert AgentFramework execution records carry the same `ProcessRunId` and `ProcessStepId` as the Process module run and step.
- Process-run proof must assert provider usage observations for the same process run and step.
- Live OpenAI execution must remain opt-in through explicit environment flags and must not log raw API key values.
- Live OpenAI execution must be bounded by explicit model, timeout, and token-ceiling settings.
- Budget ceiling failures must fail closed; high usage cannot be hidden or reclassified as success without an explicit bounded ceiling change.
- P03 must not add generic object payload dispatch, fallback selector behavior, Process Core references in drivers/modules/infrastructure, or production process-state mutations from verification host paths.

## Shallow-Pass Rejections
- Reject any proof that cites only `LiveSpecialistAgentScenarioIntegrationTests` as process-run proof.
- Reject any proof that does not include `ProcessesService.StartRunAsync` or equivalent Process module run creation.
- Reject any proof that does not include `IProcessRunAutomationDispatchService.DispatchAsync`.
- Reject any proof that omits `ExecutionRunQuery(ProcessRunId, ProcessStepId)` or equivalent process-bound execution inspection.
- Reject any proof that omits provider usage observations tied to the same process run and step.
- Reject any proof that prints an OpenAI API key or embeds a raw secret in transcript artifacts.

## Positive Proof Shape
- `LiveProcessRunOpenAiSmokeIntegrationTests` creates a dedicated PostgreSQL database, a project, a process definition, a process run, and a CRM-HR AI-agent binding to the OpenAI provider.
- The test resolves the process assignment using `ProcessExecutorKindNames.AiAgent` and then calls `IProcessRunAutomationDispatchService.DispatchAsync`.
- The test verifies completed process step status, completed AgentFramework execution state, process-run metadata, OpenAI provider/model, and provider usage observations.
- The strict skip test passes when live flags are absent, proving default test runs do not call OpenAI.
- The budget red-team transcript proves a too-small token ceiling fails the live smoke instead of silently ignoring usage.

## Gate Result
Gate C is semantically adequate for P03. It proves live OpenAI process-run dispatch without broadening production driver execution authority.
