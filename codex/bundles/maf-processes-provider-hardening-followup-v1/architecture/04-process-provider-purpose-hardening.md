# Process Provider Purpose Hardening

The process provider currently receives `AgentRuntimeToolProviderContext` but primarily uses `context.Agent` to create tools. Future manager verification and process drivers need purpose-aware behavior.

## Direction

- `InteractiveChat`: expose tools according to agent process access metadata.
- `GovernedProcessAutomation`: preserve existing governed process automation capability when access metadata grants it.
- `AutoApprovedNonInteractive`: allow only explicitly safe operations unless policy grants write and approval suppression is intentional.
- `A2AEndpoint`: default to read-only/no process tools unless explicitly enabled.

This bundle should lay groundwork and tests only. It should not introduce domain process drivers.
