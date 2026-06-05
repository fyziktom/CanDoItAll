# Target Solution

The target for this bundle is a module-local candidate hydration boundary:

```text
ProcessRunAutomationDispatchService.Dispatch.cs
  -> ProcessDispatchCandidateHeaderSelector       (EF read + header shape, module-local)
  -> ProcessDispatchCandidateHydrationLoader      (EF read snapshot, module-local)
  -> ProcessDispatchCandidateHydrationAssembler   (pure-ish candidate assembly, module-local)
  -> ProcessDispatchArtifactInputAssembler        (artifact input prompt shaping, module-local)
  -> ProcessDispatchAssignmentResolver            (current assignment/workflow route facts, module-local)
  -> ProcessDispatchTechnicalAgentBindingCoordinator (side-effect explicit, module-local)
```

The dispatcher remains the lifecycle orchestrator. It still owns:

- durable claim acquisition and release,
- route sequencing,
- workflow/subprocess/execution-client calls,
- finalizer calls,
- transition execution,
- persistence side effects not explicitly moved behind a named local coordinator.

The new helpers are not Process Core. They are staging boundaries that reduce risk before a later extraction decision.
