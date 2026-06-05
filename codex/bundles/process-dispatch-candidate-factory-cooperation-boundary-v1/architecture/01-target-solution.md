# Target Solution

## Goal

Create a candidate-construction boundary after the hydration boundary.

Current shape:

```text
LoadDispatchCandidateAsync
  -> hydration loader
  -> inline per-step candidate construction
  -> inline subprocess candidate
  -> inline workflow candidate
  -> inline direct-agent candidate
  -> inline cooperation metadata
  -> inline recovery id / manual directive integration
```

Target shape:

```text
LoadDispatchCandidateAsync
  -> ProcessDispatchCandidateHydrationLoader
  -> ProcessDispatchCandidateAssemblyContextFactory
  -> ProcessDispatchCandidateFactory
      -> BuildSubprocessCandidate
      -> BuildWorkflowCandidate
      -> BuildDirectAgentCandidate
  -> ProcessDispatchCooperationMetadataResolver
  -> ProcessDispatchTechnicalAgentBindingCoordinator
  -> ProcessDispatchRecoveryQueryHelper
```

## Important Boundary

`ProcessDispatchCandidateFactory` should construct `DispatchCandidate` objects but must not:

- load from EF,
- call executionClient,
- call technicalAgentBridge,
- mutate agent configuration,
- transition process steps,
- write process journals,
- run workflows, subprocesses, or agents.
