# Architecture Cutline

## Keep inside `CanDoItAll.Modules.Processes`

- All new projection models.
- Projection snapshot builders.
- Projection rule helpers.
- Projection facet implementations.
- Projection source coordinators.
- Dispatcher adapter from existing nested models to projection models.

## Do not create yet

- `CanDoItAll.Processes.Core`
- `CanDoItAll.Processes.DriverPacks.*`
- `IProcessDriverPack`
- cross-module projection abstractions
- public NuGet-ready process projection contracts

## Desired dependency direction

```text
ProcessRunAutomationDispatchService
  -> ProjectionAdapter/Factory
      -> Projection snapshots/state
      -> Projection orchestrator
          -> Source coordinators
              -> Facets/rules/file-io side-effect boundaries
```

Avoid:

```text
Source coordinator -> ProcessRunAutomationDispatchService nested model aliases
Source coordinator -> ProcessRunAutomationDispatchService static helper forwarding
Pure rule helper -> File/Directory/Storage/DbContext
```
