# Boundary target

Current transitional shape:

```text
ProjectExecutionArtifactsAsync
  -> ProcessArtifactProjectionOrchestrator
      -> Source coordinators
          -> IProcessArtifactProjectionHost (broad adapter)
              -> ProcessRunAutomationDispatchService methods
```

Target shape after this bundle:

```text
ProjectExecutionArtifactsAsync
  -> ProcessArtifactProjectionOrchestrator
      -> Source coordinators
          -> small module-local facets/services
              -> explicit dispatcher adapter only where unavoidable
```

The key goal is not to make these facets public contracts. They remain internal to `CanDoItAll.Modules.Processes`.
