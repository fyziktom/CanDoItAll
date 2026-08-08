# Target file map

## Core/runtime

- `AgentTurnContextMetadata.cs`
- `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `AgentToolInvocationPolicyPipeline.cs`
- `AgentFrameworkWorkspaceExecutionService.ProcessLeases.cs`
- workspace process lease/command service contracts

## Modules

- authority resolver and source authority implementations/registrations
- Processes policy contributor and source authority provider
- Workbench/Projects source authority providers
- AgentFramework workspace factory and module registration

## Ordinary LLM

- `LlmConversationContracts.cs`
- `FileLlmConversationStore.cs`
- `LlmConversationService.cs`
- `LlmInvocationContracts.cs`
- `ProviderBackedLlmInvocationAdapter.cs`
- `WorkflowLlmComponentInvoker.cs`

## Avoid without a new failing test

- MAF provider-agent creation;
- runtime-state envelope v2;
- process artifact recovery;
- floating context capture;
- approval continuation protocol.
