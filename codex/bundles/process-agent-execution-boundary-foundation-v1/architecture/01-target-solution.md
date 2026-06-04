# Target Solution

## End State After This Bundle

```mermaid
flowchart LR
    Dispatcher[ProcessRunAutomationDispatchService]
    ExecClient[IProcessAutomationExecutionClient]
    AF[AgentFramework Workspace Service]
    MAF[MAF Runtime]
    Providers[Runtime Tool Providers]

    Dispatcher --> ExecClient
    ExecClient --> AF
    AF --> MAF
    MAF --> Providers
```

The dispatcher remains in `CanDoItAll.Modules.Processes`, but its direct AgentFramework execution calls are hidden behind a process automation execution boundary. This is a staging seam, not the final clean core.

## Boundary Rules

- MAF stays product-tool-neutral.
- Tooling stays product-neutral.
- Processes may still reference AgentFramework Core during this staging bundle, but only through a small client/facade after SB06.
- New contracts/abstractions must not reference Razor, EF, MAF, Workbench, or provider-specific implementation packages.
- Receipt and artifact validation stay behaviorally identical.

## Final Desired Direction Later

```mermaid
flowchart LR
    ProcessCore[Future Processes.Core]
    ProcessApp[Processes Application]
    ExecGateway[IProcessAgentExecutionGateway]
    AFIntegration[AgentFramework Integration]
    MAF[MAF Runtime]

    ProcessCore --> ProcessApp
    ProcessApp --> ExecGateway
    AFIntegration --> ExecGateway
    AFIntegration --> MAF
```

This later direction is not fully implemented by this bundle.
