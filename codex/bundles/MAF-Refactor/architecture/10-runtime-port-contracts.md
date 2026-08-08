# Target agent-runtime and lightweight-inference port contracts

The names below are a target shape. SB09 may adjust naming after dependency analysis, but it must preserve the separation.

## Execution

```csharp
public interface IAgentExecutionRuntime
{
    Task<AgentRuntimeExecutionResult> ExecuteAsync(
        AgentRuntimeExecutionRequest request,
        CancellationToken cancellationToken = default);
}
```

`AgentRuntimeExecutionRequest` should contain immutable runtime-neutral inputs:

- agent and provider snapshots;
- model;
- capability/tool/context contribution plan;
- workspace runtime services lease;
- turn context reference and request-scoped lease;
- execution authority record;
- output/finalizer contract;
- execution budget;
- progress observer;
- runtime-state compatibility requirements.

It must not contain `IServiceProvider`, UI components, EF contexts, product module services, or MAF SDK objects.

## Continuation

```csharp
public interface IAgentContinuationRuntime
{
    Task<AgentRuntimeExecutionResult> ContinueAsync(
        AgentRuntimeContinuationRequest request,
        CancellationToken cancellationToken = default);
}
```

The request carries:

- execution run identity;
- original turn context reference/lease;
- original authority fingerprint;
- versioned runtime-state envelope;
- stable per-proposal approval decisions;
- provider/model/toolset compatibility evidence.

It does not capture current UI context.

## Provider diagnostics

```csharp
public interface IProviderDiagnosticsRuntime
{
    Task<ProviderHealthResult> TestHealthAsync(...);
    Task<ProviderTestChatResult> RunProbeAsync(...);
}
```

Diagnostics does not require an agent workspace, chat session, process policy, or execution run.

## Provider model administration

```csharp
public interface IProviderModelAdministrationRuntime
{
    Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(...);
}
```

Model administration has independent authorization, logging, and tests.

## Hosted agent factory

Expose this only for A2A/hosting paths that need a framework-native hosted agent lifetime. It returns an owned lease and must not be the ordinary application execution interface.

## Separate lightweight LLM invocation boundary

This contract is not part of `AgentFramework.Runtime.Abstractions`. It belongs in the separate SDK-free `AgentFramework.Llm.Abstractions` boundary defined by ADR-010. It is documented here only to show its relationship to agent execution.


```csharp
public interface ILlmInvocationPort
{
    Task<LlmInvocationResult> InvokeAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default);
}
```

This port has no tools, memory, agent session, floating context, handoff, or product authority. Specialized workflow agent nodes use the execution runtime instead.

## Agent runtime and provider-backed implementation ownership

Suggested collaborators:

```text
MafAgentExecutionAdapter
MafAgentContinuationAdapter
MafStreamingTurnExecutor
MafRuntimeBuildFactory
MafRuntimeResponseMapper
MafRuntimeStateAdapter
MafProviderDiagnosticsAdapter
MafProviderModelAdministrationAdapter
MafHostedAgentFactory

ProviderBackedLlmInvocationAdapter  # provider runtime/driver layer, not MAF agent execution
```

The lightweight LLM adapter is not a MAF agent collaborator and must not construct agent/session/capability state. No collaborator should become another universal runtime manager. Each extracted responsibility needs direct unit tests and an old-owner deletion proof.
