# Tool Provider Runtime Sequence

```mermaid
sequenceDiagram
    autonumber
    participant Exec as AgentFramework execution service
    participant Maf as MafAgentRuntime
    participant Registry as DI IEnumerable<IAgentRuntimeToolProvider>
    participant ProcProvider as ProcessAgentRuntimeToolProvider
    participant ProcServices as ProcessesService + Template Services
    participant Policy as Agent tool policy
    participant Provider as AI Provider

    Exec->>Maf: RunAsync(agent, provider, capabilities, prompt, options)
    Maf->>Maf: CreateCapabilityComposition(...)
    Maf->>Registry: Resolve ordered runtime tool providers
    Registry-->>Maf: Providers, maybe empty
    Maf->>ProcProvider: CreateToolsAsync(context)
    ProcProvider->>ProcServices: Resolve process/template data only when tools invoked
    ProcProvider-->>Maf: AITool list
    Maf->>Policy: Apply approval wrappers / classify tools
    Maf->>Maf: Deduplicate tool names and attach to ChatOptions
    Maf->>Provider: Execute agent call
    Provider-->>Maf: tool calls / output
    Maf-->>Exec: runtime response with receipts/traces
```

## Required Behavior

- Tool provider resolution must be deterministic.
- A provider failure during composition must fail predictably with provider name and diagnostic message.
- A provider returning duplicate names must not silently shadow a previously registered tool.
- The same approval wrapper semantics must apply to process tools after migration.
- Provider-based composition must not require process services when Processes module is absent.
