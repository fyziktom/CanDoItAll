# Current-State Analysis

## Process Projects

The current repository already has multiple Process-related projects and folders:

- `src/CanDoItAll.Modules.Processes`: Blazor UI, application service, persistence entities, templates, runtime, dispatch, recovery, launch planning, observation, and canvas logic.
- `src/CanDoItAll.Processes.Contracts`: minimal stable contracts for runtime host boundaries.
- `src/CanDoItAll.Processes.Core`: extracted pure-ish rules for artifacts, routing, execution evidence, finalization, diagnostics, and subprocess lifecycle.
- `src/CanDoItAll.Processes.Drivers.Abstractions`: verification-oriented driver contracts.
- `src/CanDoItAll.Processes.Drivers.*`: domain verification drivers for transcript, runtime evidence, artifact evidence, Office evidence, business analysis, observation aggregation, and verification gateway.
- `Templates/Processes`: current JSON/Markdown/Mermaid process template pack.

This split is a useful start, but it is not the target architecture. The UI module still owns too much runtime behavior.

## Current Definition And Runtime Models

`repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs` contains definition entities for:

- `ProcessDefinition`
- `ProcessDefinitionVersion`
- `ProcessRoleRequirement`
- `ProcessStepDefinition`
- `ProcessStepDependencyDefinition`
- `ProcessStepBranchOutcomeDefinition`
- `ProcessStepRoleAssignmentRequirement`
- `ProcessArtifactExpectation`
- `ProcessStepArtifactInputDefinition`

`repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs` contains runtime entities for:

- `ProcessRun`
- `ProcessStepRun`
- `ProcessRunAssignment`
- `ProcessWorkBrief`
- `ProcessDecisionRecord`
- `ProcessArtifactRecord`
- `ProcessJournalEntry`
- `ProcessConformanceObservation`
- `ProcessImprovementCandidate`
- `ProcessLaunchPlan`
- `ProcessWorkflowRunLink`

These models show the current system already knows about roles, assignments, steps, artifacts, subprocess parent links, decisions, journals, launch plans, and workflow links. The weakness is not the absence of concepts. The weakness is that the concepts are not cleanly separated into definition, instance composition, runtime state, event stream, artifact ledger, and UI projection.

## Current Runtime And Dispatcher

`repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs` is the main automation dispatcher. It directly depends on EF, service scope factory, technical agent bridge, execution client, storage placement/catalog/driver services, workspace path resolver, database profile accessor, workflow coordinator, options, clock, and logger.

The dispatcher is a large partial service spread across many files. It includes:

- direct agent execution,
- workflow execution,
- subprocess runtime handling,
- route handling,
- artifact projection,
- artifact validation,
- recovery directive generation,
- provider repair,
- browser proof,
- dotnet run cleanup,
- tool validation,
- implementation proof checks,
- finalizer logic,
- route execution,
- exception closure,
- concurrency and claim lifecycle.

This service contains useful hard-won behavior, but as architecture it is a red flag. It is too close to an application service and too far from a generic process scheduler.

## Current Core Extraction

`repo://src/CanDoItAll.Processes.Core` already contains useful pure rule examples:

- `Artifacts/ProcessArtifactExpectationMatcher.cs`
- `Artifacts/ProcessCoreArtifactModels.cs`
- `Routing/ProcessDispatchRoutePlanner.cs`
- `Routing/ProcessDispatchRoutePipeline.cs`
- `Subprocess/ProcessSubprocessLifecycleRules.cs`

These are small, testable, mostly dependency-free rules. They should be treated as examples of the desired direction, not as a complete core.

## Current Driver Layer

`repo://src/CanDoItAll.Processes.Drivers.Abstractions` currently centers on verification:

- `ProcessDriverVerificationRequest`
- `ProcessDriverVerificationResponse`
- `ProcessDriverOperation`
- `ProcessDriverCapabilityScope`
- `ProcessDriverPermissionMode`
- evidence, audit, and redaction descriptors

`repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs` directly wires specific verifier classes for transcript, runtime evidence, artifact evidence, Office evidence, business analysis, and observation aggregation.

This is useful but insufficient. The future driver system needs discovery, layering, capability matching, strategy provision, branch definition provision, artifact recovery provision, and manager-policy provision. Current drivers mostly answer read-only verification questions.

The abstraction also currently exposes domain-specific scope values such as `DotNetRustTranscriptVerification`, `OfficeEvidenceRead`, and `BusinessAnalysisRead` in the driver abstractions. That is acceptable in a driver contract package if treated as driver-contributed capability IDs, but not acceptable inside the generic runtime core.

## Current Templates

`repo://Templates/Processes/manifest.json` defines the current template pack with version `2.1.0-live-run-governance`.

`repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs` loads:

- manifest,
- framework sources,
- toolbox role/step templates,
- chrome actions,
- baseline scenarios,
- live run profiles,
- shared roles/artifacts/checklists/validations/prompts,
- process-local roles/artifacts/checklists/validations/prompts,
- process definitions.

`repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateProjectionService.cs` projects template pack files into current module import envelopes.

The current direction is good: JSON templates with shared and local resources. The missing pieces are schema migrations, component override tracking, conflict detection, update publication, and a clean canonical/projection split.

## Current Monitoring

`repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs` provides a memory cache keyed by project, definition, run, step, query fingerprint, and cache kind.

`repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` builds dashboard, live, run, stage, timeline, and dialog snapshots.

`repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor` shows the current live-process UX direction: history window selector, process filter, refresh, summary stats, activity, agents, graphs, and tool analytics.

This UX should be preserved. The backend implementation should be replaced with typed runtime events and asynchronous snapshot projection.

## Current Branching

`repo://src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasBranching.cs` defines default and error branch outcomes, branch-router rendering, branch node IDs, and branch normalization.

`repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBranchOutcomeRouting.cs` detects exception routing by system error outcome or text-token heuristics such as repair, rework, defect, failed validation, exception, and blocked.

This is not strong enough for the target architecture. Branch definitions need typed outcome semantics, branch input contracts, manager decision records, domain-provided branch families, user overrides, backward routing, and loop budgets.

## Current Subprocess Handling

`repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` already supports parent run, parent step run, root run, hierarchy depth, parent/child validation, existing subprocess run reuse, and subprocess definition cycle checks.

This is a good concept. The gap is that subprocess construction is still runtime service behavior rather than a recursive instance-composition concern. The target architecture must build subprocess instance plans through the same builder pipeline as root processes.

## Current Recovery

`repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBlockStateClassifier.cs` maps block reasons to recovery options.

`repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs` chooses a next recovery action and detects repeated no-progress evidence fingerprints.

`repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/AgentRecoveryModels.cs` contains useful concepts such as recovery mode, failure category, session strategy, rework packets, proof requirements, reusable proof refs, recovery ledger entries, and loop decisions.

The target architecture should keep the ideas, but move them behind generic manager and recovery strategy interfaces. Current recovery logic is too agent-specific and too coupled to the dispatcher.

