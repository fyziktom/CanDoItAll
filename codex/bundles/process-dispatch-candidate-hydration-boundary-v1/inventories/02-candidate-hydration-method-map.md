# Candidate Hydration Method Map

Live source captured during execution. Current source references are under `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`.

| Method / region | Source file | Category | Side effects | Planned treatment |
| --- | --- | --- | --- | --- |
| `LoadDispatchCandidateHeadersAsync` wrapper | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Candidate header selection | EF read delegated to selector | Delegates to `ProcessDispatchCandidateHeaderSelector.SelectAsync`. |
| `ProcessDispatchCandidateHeaderSelector.SelectAsync` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHeaderSelector.cs` | Candidate header selection | EF read only | Owns run eligibility, step status, lease expiry, and sequence ordering. |
| `ProcessDispatchCandidateHydrationLoader.LoadAsync` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs` | Hydration read model | EF read only | Builds `ProcessDispatchCandidateHydrationSnapshot` for run, definition, steps, work briefs, assignments, artifacts, branch outcomes, and artifact inputs. |
| Branch outcome / conditional dependency shaping | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchBranchDependencyContext.cs` | Candidate shaping | none after read | Creates typed branch outcomes and `RequiresExplicitBranchOutcomeSelection`. |
| Artifact-input construction and prompt path shaping | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchArtifactInputAssembler.cs` | Prompt shaping | path preparation delegated by wrapper | Builds upstream artifact inputs and applies prepared managed paths through an explicit delegate. |
| Workflow/current assignment recognition | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchAssignmentRouteHelper.cs` | Assignment route shaping | none | Resolves current assignment and workflow-backed routes. |
| Direct-agent recoverable execution selection | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs` | Candidate execution facts | consumes execution-client records; journal read for directive | Wraps recoverable execution selection and manual recovery directive query. |
| Technical-agent binding and project-structure read access mutation | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs` | Side-effectful binding coordinator | bridge read, editor read/write | Explicit outcome for missing binding, unchanged binding, existing access, or granted/saved access. |
