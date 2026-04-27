# 04 - Finalizer Tools for Critical Decisions

## Objective

Implement exact-once typed finalizer tools for selected critical workflow decisions. Structured final responses are good, but finalizer function calls are stronger when a decision must be submitted exactly once and captured as typed arguments.

## Primary files to inspect


- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/CanDoItAll.AgentFramework.Models/OutputContracts/AgentOutputContracts.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.*.cs`
- `docs/agent-output-contracts.md`


## Required implementation tasks


1. Define a finalizer policy abstraction:
   - finalizer required or optional
   - expected finalizer tool name
   - expected DTO type
   - exact-once enforcement
2. Implement finalizer result capture for at least one high-value target:
   - `SubmitProcessStepOutcome(ProcessStepOutcomeResult result)` in shadow mode or full mode, or
   - `SubmitProcessStatePatch(ProcessStatePatch patch)`, or
   - deployment/security/tool decision finalizer if such a path already exists.
3. Register finalizer tools through `AIFunctionFactory.Create(...)` or the installed MAF equivalent.
4. Ensure finalizer tool calls are validated through the same validator registry.
5. Treat normal assistant text as display-only when finalizer mode is required.
6. Missing finalizer call, multiple finalizer calls, malformed finalizer arguments, or mismatched finalizer type must fail validation.
7. Add finalizer status to run diagnostics.
8. Update prompts for finalizer-enabled agents.


## Required tests


Unit tests:
- Required finalizer missing -> failure.
- Required finalizer called once with valid DTO -> success.
- Required finalizer called multiple times -> failure.
- Finalizer called with malformed DTO -> failure.
- Assistant text cannot override finalizer result.

Integration tests:
- A process decision or selected critical decision is captured through finalizer and validates.
- Invalid finalizer arguments prevent completion.


## Risks and constraints


- Finalizer tools may interact with approval/tool-call loops. Ensure finalizer tools do not require human approval unless that is explicitly desired.
- Avoid side effects inside finalizer tools until validation succeeds.

