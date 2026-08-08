# C# dependency direction

`A -> B` means project A has a compile-time reference to project B.

## Current high-risk references

### MAF project

Current project file:

`src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`

High-risk references include:

```text
AgentFramework.Maf -> Modules.Security
AgentFramework.Maf -> Modules.Workspace
AgentFramework.Maf -> Workflows.MafAdapter
AgentFramework.Maf -> Tools.Documents implementation
AgentFramework.Maf -> Infrastructure storage surface
```

Immediate target:

```text
AgentFramework.Maf -X-> Modules.*
```

The Workflows.MafAdapter reference must also be removed by relocating MAF-native handoff construction or introducing a correctly directed adapter seam.

### Processes module

Current direction:

```text
Modules.Processes -> Modules.AgentFramework
Modules.Processes -> AgentFramework.Core
Modules.Processes -> AgentFramework.Models
```

This direction permits Processes to implement generic AgentFramework policy/recovery contracts. Do not reverse it.

### Workbench module

Current direction:

```text
Modules.Workbench -> AgentFramework.Core
Modules.Workbench -> AgentFramework.Models
Modules.Workbench -> AgentFramework.Components
Modules.Workbench -> AgentFramework.Tooling
```

This is appropriate for publishing UI observations and implementing product tools, provided Core does not reference Workbench.

## Target graph

```text
AgentFramework.Models
    ^
    |
AgentFramework.Runtime.Abstractions (optional, SDK-free)
    ^                    ^
    |                    |
AgentFramework.Core      AgentFramework.Maf
    ^                    ^
    |                    |
Modules.Workbench        Hosting / composition
Modules.Processes        Modules.AgentFramework composition
Modules.Security
```

More explicitly:

```text
Core -> Models
Core -> Runtime.Abstractions

Maf -> Models
Maf -> Runtime.Abstractions
Maf -> Core-owned narrow workspace/tool abstractions when still required
Maf -> Security.Abstractions
Maf -X-> Modules.*

Workbench -> Models/Core/Context contracts
Processes -> Models/Core/Runtime contracts
Security implementation -> Security.Abstractions

Hosting -> Core
Hosting -> Maf
Hosting -> Workflows.MafAdapter
Application composition -> product modules
```

## Lightweight LLM dependency graph

The application-facing inference boundary is deliberately below agent execution and above provider protocol drivers:

```text
Workflows.Core / workflow LLM executor -> Llm.Abstractions
Future ordinary-chat application      -> Llm.Abstractions
Llm.ProviderRuntime implementation     -> Llm.Abstractions
Llm.ProviderRuntime implementation     -> AgentFramework.Providers / provider runtime abstractions
Composition root                       -> Llm.ProviderRuntime implementation

Llm.Abstractions -X-> AgentFramework.Maf
Llm.Abstractions -X-> AgentFramework.Core implementation
Llm.Abstractions -X-> Modules.* / UI / provider SDK packages
Workflow ordinary LLM path -X-> IAgentExecutionRuntime / IAgentRuntime
Future ordinary chat -X-> AgentDefinition / ChatSessionRecord / MAF session state
```

The existing `IProviderChatCompletionDriver` and provider runtime pool are implementation foundations, not the application contract. Workflow and future chat code should not select concrete provider drivers or runtime handles directly. The focused provider-backed adapter performs that mapping.

If ordered-message, streaming, attachment, response-format, or usage support requires provider-contract expansion, keep the change additive and test every concrete provider driver. Do not add a parallel provider client in the workflow or LLM project.

## Forbidden references

Add architecture tests that fail on:

1. Any `ProjectReference` from `CanDoItAll.AgentFramework.Maf.csproj` containing `\Modules\`.
2. Any `using CanDoItAll.Modules.` under `src/MAF/Common/CanDoItAll.AgentFramework.Maf`.
3. Any `ProcessStepOutcomeResult`, `ProcessStepOutcomeStatus`, `ProcessArtifactRecovery`, or literal `"process-step"` under the MAF project after SB13.
4. Any reference from Core to MAF.
5. Any MAF SDK namespace in Runtime/Context abstractions.
6. Any Workbench or Processes reference to a concrete MAF implementation.
7. Any new `BuildServiceProvider()` in registration code.
8. Any new runtime field of type `IServiceProvider`.
9. Any reference from `Llm.Abstractions` to MAF, product modules, UI, agent-session contracts, or provider SDKs.
10. Any ordinary workflow or ordinary-chat caller of `IAgentRuntime` / `IAgentExecutionRuntime`.
11. Any lightweight implementation that creates its own provider credentials, HTTP clients, dispatch lane, retry stack, or usage parser instead of using the provider runtime.

## Security abstraction extraction

Move or duplicate-then-migrate these contracts from `Modules.Security` to a narrow abstraction project:

- `ISecretRuntimeResolver`
- `SecretRuntimeRequest`
- secret purpose/consumer identifiers needed by MAF/MCP

Then:

```text
Maf -> Security.Abstractions
Modules.Security -> Security.Abstractions
Hosting -> Modules.Security
```

Delete the old duplicate contract after all callers migrate.

## Workspace reference audit

Search the MAF project for actual usage from `Modules.Workspace`.

- If no source usage exists, remove the stale project reference and prove build/tests.
- If usage exists, identify the exact interface.
- Move that interface to a lower-level abstraction project.
- Do not keep the module reference for convenience.

## Handoff location decision

Current common MAF runtime depends on `Workflows.MafAdapter` for MAF-native handoff construction.

Preferred resolution:

- move `MafHandoffWorkflowFactory` and its MAF-native guard to `AgentFramework.Maf/Runtime/Handoffs`,
- keep stored workflow compilation in `Workflows.MafAdapter`,
- remove `AgentFramework.Maf -> Workflows.MafAdapter`.

Alternative:

- introduce a very narrow adapter contract in a lower MAF-specific abstractions project,
- inject the implementation from composition.

Reject an abstraction that exposes many MAF SDK types merely to preserve the current file location.

## Dependency proof

Every project-reference change requires:

- before/after `.csproj` table,
- CodeAnalytics dependency/cycle output when available,
- direct source assertions,
- targeted project builds,
- solution build,
- registration smoke,
- explanation for every new reference;
- proof that the workflow/lightweight path reaches the existing provider runtime exactly once;
- proof that `Llm.Abstractions` remains SDK-free and agent-free.
