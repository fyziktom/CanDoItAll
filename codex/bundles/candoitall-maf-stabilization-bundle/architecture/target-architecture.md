# Target Architecture

## Stabilization principle

The process engine owns state and transitions. Agents perform bounded reasoning and tool use inside a controlled runtime. MAF is the runtime layer for agents, tools, middleware, context, sessions, approvals, and selected workflow orchestration.

## Target runtime flow

```text
Process Engine / Workflow Owner
  -> creates AgentExecutionRequest
      - AgentId
      - ProcessInstanceId
      - StepId
      - SourceKind
      - StructuredOutputContract
      - ToolPolicyProfile
      - FinalizerPolicy
      - SessionPolicy
      - ProviderCapabilityRequirements
  -> AgentExecutionService
      - validates provider capabilities
      - builds bounded context
      - restores/creates session
      - configures MAF agent pipeline
  -> MAF Agent Pipeline
      - agent-run middleware
      - context providers
      - chat-client middleware
      - function invocation middleware
      - function tools / MCP / hosted tools
      - approval handling
  -> Structured response or finalizer tool result
  -> Output validator registry
  -> bounded repair/retry if enabled
  -> policy/security validation
  -> persistence as validated run detail / process event
  -> process transition or human escalation
```

## Core invariants

1. A model may suggest; the process engine decides.
2. Human-readable markdown is display-only.
3. Tool calls are governed before execution.
4. Destructive actions require explicit policy allow or approval.
5. Typed output is validated before persistence.
6. Repaired output is never trusted until revalidated.
7. Sessions are not process state.
8. Provider/model capabilities are checked before run start.
9. Domain-specific recovery guidance is not stored in the generic runtime.
10. Every run is traceable.

## Key service abstractions to add or harden

```csharp
public interface IAgentOutputValidatorRegistry
{
    bool TryResolve(Type outputType, out IAgentOutputValidator validator);
}

public interface ITypedAgentExecutionService
{
    Task<TypedAgentExecutionResult<TOutput>> RunAsync<TOutput>(
        TypedAgentExecutionRequest<TOutput> request,
        CancellationToken cancellationToken);
}

public interface IAgentToolInvocationPolicy
{
    Task<ToolInvocationPolicyDecision> EvaluateAsync(
        ToolInvocationPolicyContext context,
        CancellationToken cancellationToken);
}

public interface IAgentFinalizerPolicy
{
    bool IsFinalizerRequired(AgentExecutionPolicyContext context);
    string? RequiredFinalizerToolName { get; }
}

public interface IProviderCapabilityProfileService
{
    ProviderCapabilityProfile Resolve(ProviderProfile provider, string model);
}

public interface IAgentContextSnapshotProvider
{
    Task<AgentContextSnapshot> BuildSnapshotAsync(
        AgentExecutionPolicyContext context,
        CancellationToken cancellationToken);
}
```

## Preferred decision boundaries

| Concern | Owner |
|---|---|
| Process status | Process engine after validated agent output |
| Branch routing | Process engine after validated branch outcome |
| Tool execution permission | Tool policy middleware + approval wrapper |
| Prompt instructions | Agent profile/template, not business source of truth |
| Run persistence | Execution service after validation |
| Session serialization | Runtime/session manager |
| Model capability compatibility | Provider capability service |
| Human approval | Tool approval or workflow HITL port/request |
| Domain-specific hints | Process template, skill, or recovery directive provider |
