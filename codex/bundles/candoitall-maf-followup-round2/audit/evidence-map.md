# Evidence map

This map points Codex to the concrete repository files and line ranges inspected in the latest snapshot.

## Good current behavior

### Required finalizer policy for governed process steps

File:

```text
src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:99-134
```

Evidence:

- Builds `ExecutionInvocationPolicy(FinalizerMode: AgentFinalizerMode.Required, ...)`.
- Builds metadata through `ExecutionInvocationMetadata.Build(...)`.
- Passes `StructuredOutput: ProcessStepOutcomeStructuredOutputContract`.

### Validation before assistant message persistence

File:

```text
src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:617-630
src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:173-186
```

Evidence:

- `ValidateMachineOutputBeforeCompletionAsync(...)` is called before assistant `ChatMessageRecord` creation on initial and approval-continuation paths.

### Required finalizer output replaces response text

File:

```text
src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:1002-1084
```

Evidence:

- Resolves `finalizerMode` from run metadata.
- Validates matching finalizer invocation.
- In required mode, serializes finalizer output and returns `response with { ResponseText = finalizerOutputJson }`.

### Process implicit completion disabled

File:

```text
src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs:89-96
```

Evidence:

- `CanImplicitlyCompleteGovernedStep(...)` returns `false`.

## Remaining issues

### Runtime attaches finalizer based only on structured output

File:

```text
src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:44-84
src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:413-463
```

Evidence:

- `CreateRuntimeBuildAsync(...)` receives only `AgentStructuredOutputContract? structuredOutput`.
- `CreateFinalizerCapture(structuredOutput)` is called at runtime-build time.
- If a known contract is found, finalizer tools are added before the execution service resolves effective finalizer mode.

Related finalizer mode resolution:

```text
src/CanDoItAll.AgentFramework.Core/Finalizers/AgentFinalizerPolicy.cs:88-108
```

- `ResolveMode(...)` reads `agentFinalizerMode` from run metadata and otherwise defaults process step runs to shadow and others to disabled.

### Finalizer instruction conflict

File:

```text
src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:466-483
```

Evidence:

- Appends exact-once finalizer instruction.
- Also says normal assistant text is display-only, despite the run using JSON-schema `ResponseFormat` for structured-output responses.

### Tool policy exception boundary

File:

```text
src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:360-380
src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:499-500
```

Evidence:

- Policy branches throw `InvalidOperationException`.
- `IsPolicyException(...)` treats all `InvalidOperationException` and `NotSupportedException` as policy exceptions.
- Ordinary tool errors can be misreported as policy blocks.

### Core provider feature matrix is improved

File:

```text
src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs:136-181
```

Evidence:

- OpenAI/Azure OpenAI Responses and Chat Completions are JSON-schema structured-output capable.
- Tool approval requests/wrappers are restricted to Responses transport.
- Ollama is not structured-output capable.

### Workspace UI defaults still disagree with core feature matrix

Files:

```text
src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor.cs:196-205
src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs:327-335
```

Evidence:

- Both pages set `OllamaProviderAdapter` and `OllamaRemoteProviderAdapter` structured-output defaults to `true`.

### Workspace-backed registry still stores structured-output support by transport only

File:

```text
src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs:136-140
```

Evidence:

- `entity.SupportsStructuredOutput = model.Transport == ProviderTransportKind.Responses;`
- This conflicts with the core matrix, where OpenAI/Azure Chat Completions can support JSON-schema structured output.

### Managed SQLite bootstrap persists misleading structured-output flag

File:

```text
src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs:394-408
src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs:568-578
src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs:620-630
```

Evidence:

- Workspace provider DB seed sets `SupportsStructuredOutput = false` for managed SQLite OpenAI.
- Refresh logic forces the persisted flag false.
- Core in-memory provider uses OpenAI Chat Completions, which the core feature matrix marks as structured-output capable.

### Hardening static tests exist but do not catch the remaining failures

File:

```text
tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs:45-57
```

Evidence:

- Tests check that `RequireApproval` logic exists but do not verify that ordinary tool exceptions are not wrapped as policy blocks.
- There is no visible test proving finalizer tool composition is mode-aware.
