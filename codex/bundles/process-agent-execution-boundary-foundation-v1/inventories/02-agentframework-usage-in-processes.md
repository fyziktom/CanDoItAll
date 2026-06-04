# AgentFramework Usage In Processes Inventory

Generated in SB02 before production movement. Source transcripts: bundle://proof/SB02/transcripts/agentframework-usage-scan.txt, bundle://proof/SB02/transcripts/direct-execution-call-scan.txt, and bundle://proof/SB02/transcripts/dispatcher-partial-line-counts.txt.

## Summary

- Dispatcher direct execution/readback calls are concentrated in `ProcessRunAutomationDispatchService.Execution.cs`, with additional dispatcher readbacks in `Concurrency.cs`, `Grounding.cs`, `Costing.cs`, `CompletionArtifactRecovery.cs`, and `Dispatch.cs`.
- Non-dispatcher direct calls exist in manager chat, observation services, recovery worker, and process UI loaders. Those are recorded but remain outside the SB06 dispatcher execution-path move.
- The first production move should introduce a process-owned execution client facade and route dispatcher execution start/detail/adoption/recovery calls through it before any Process Core extraction.
- Browser validation remains N/A for this inventory-only subbundle.

## Direct Execution Call Inventory

| Source | Usage kind | Dispatcher | Call excerpt | Proposed owner after this bundle |
| --- | --- | --- | --- | --- |
| repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs:118 | execution-run query | No | var executionRuns = await workspaceService.ListExecutionRunsAsync( | Out of dispatcher-boundary scope for this bundle |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs:125 | execute | No | var result = await workspaceService.ExecuteRunAsync( | Out of dispatcher-boundary scope for this bundle |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs:260 | execute | No | var result = await AgentWorkspaceService.ExecuteRunAsync( | Out of dispatcher-boundary scope for this bundle |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs:450 | execution-run query | No | return (await workspaceService.ListExecutionRunsAsync( | Out of dispatcher-boundary scope for this bundle |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs:479 | detail/readback | No | details.Add(await workspaceService.GetExecutionRunDetailAsync( | Out of dispatcher-boundary scope for this bundle |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs:285 | execution-run query | Yes | var executionRuns = await workspaceService.ListExecutionRunsAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs:306 | detail/readback | Yes | var detail = await workspaceService.GetExecutionRunDetailAsync(run.Id, cancellationToken); | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:61 | detail/readback | Yes | detail = await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken); | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:111 | execute | Yes | executionResult = await workspaceService.ExecuteRunAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:145 | detail/readback | Yes | failedExecutionDetail = await workspaceService.GetExecutionRunDetailAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:164 | detail/readback | Yes | failedExecutionDetail = await workspaceService.GetExecutionRunDetailAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:223 | detail/readback | Yes | detail = await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken); | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:522 | execution-run query | Yes | var executionRuns = await workspaceService.ListExecutionRunsAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:535 | detail/readback | Yes | historicalDetails.Add(await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken)); | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs:1878 | execution-run query | Yes | var executionRuns = await workspaceService.ListExecutionRunsAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs:33 | execution-run query | Yes | var executionRuns = await workspaceService.ListExecutionRunsAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs:46 | detail/readback | Yes | executionRunDetails.Add(await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken)); | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:119 | execution-run query | Yes | var executionRuns = await workspaceService.ListExecutionRunsAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:131 | detail/readback | Yes | var detail = await workspaceService.GetExecutionRunDetailAsync(blockingExecutionRunId.Value, cancellationToken); | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:135 | detail/readback | Yes | detail = await workspaceService.GetExecutionRunDetailAsync(blockingExecutionRunId.Value, cancellationToken); | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:149 | execution-run query | Yes | var executionRuns = await workspaceService.ListExecutionRunsAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs:62 | detail/readback | Yes | var previousExecutionDetail = await workspaceService.GetExecutionRunDetailAsync( | Process automation execution client facade where execution-path related |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs:67 | execution-run query | No | var activeExecutionRunsByRunId = (await workspaceService.ListExecutionRunsAsync( | Out of dispatcher-boundary scope for this bundle |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs:124 | execution-run query | No | var executionRuns = await workspaceService.ListExecutionRunsAsync( | Out of dispatcher-boundary scope for this bundle |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs:161 | detail/readback | No | var detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken); | Out of dispatcher-boundary scope for this bundle |

## AgentFramework Usage By File

| File | Line count | Usage kind | Dispatcher | Proposed owner after this bundle |
| --- | ---: | --- | --- | --- |
| repo://src/CanDoItAll.Modules.Processes/_Imports.razor | 10 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolDtos.cs | 79 | runtime tool provider | No | Process runtime tool provider remains owner |
| repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.Access.cs | 255 | runtime tool provider | No | Process runtime tool provider remains owner |
| repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs | 159 | runtime tool provider | No | Process runtime tool provider remains owner |
| repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.Definitions.cs | 196 | runtime tool provider | No | Process runtime tool provider remains owner |
| repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.Policy.cs | 41 | runtime tool provider | No | Process runtime tool provider remains owner |
| repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.Runs.cs | 169 | runtime tool provider | No | Process runtime tool provider remains owner |
| repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.Templates.cs | 141 | runtime tool provider | No | Process runtime tool provider remains owner |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessBrowserProofValidator.cs | 448 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactKinds.cs | 124 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs | 1700 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs | 3934 | receipt interpretation | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.BrowserProof.cs | 576 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs | 935 | detail/readback | Yes | Process automation execution client facade |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs | 1478 | detail/readback | Yes | Process automation execution client facade |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Cooperation.cs | 288 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs | 128 | detail/readback | Yes | Process automation execution client facade |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs | 239 | finalizer parsing | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DecisionServices.cs | 229 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs | 2057 | detail/readback | Yes | Process automation execution client facade |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DomainRecoveryGuidance.cs | 41 | receipt interpretation | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs | 590 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs | 738 | execute | Yes | Process automation execution client facade |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs | 1273 | prompt metadata | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs | 781 | finalizer parsing | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedOutcomes.cs | 661 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs | 920 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs | 846 | detail/readback | Yes | Process automation execution client facade |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs | 1222 | receipt interpretation | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.InvocationMetadataBuilder.cs | 80 | prompt metadata | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs | 157 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OutputValidation.cs | 155 | finalizer parsing | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs | 951 | receipt interpretation | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProviderRecovery.cs | 84 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs | 409 | receipt interpretation | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs | 482 | receipt interpretation | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | 2138 | receipt interpretation | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs | 1993 | receipt interpretation | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs | 578 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs | 461 | AgentFramework model/core usage | Yes | Dispatcher remains owner in this bundle; revisit after facade stabilizes |
| repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/AgentRecoveryModels.cs | 844 | receipt interpretation | No | Recovery worker remains owner; candidate for later boundary review |
| repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs | 282 | detail/readback | No | Recovery worker remains owner; candidate for later boundary review |
| repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor | 3328 | finalizer parsing | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor | 161 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessExecutionRunDisplayProjector.cs | 136 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.CanvasState.cs | 221 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Launch.cs | 306 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs | 612 | execute | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs | 416 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RunsPresenter.cs | 644 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RuntimeOperations.cs | 891 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.StepsPresenter.cs | 378 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs | 894 | detail/readback | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsArtifactsSection.razor | 224 | finalizer parsing | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsExecutionSection.razor | 374 | UI projection | No | Processes UI remains owner; out of dispatcher-boundary scope |
| repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessContractCatalog.cs | 58 | process model/editor contract | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEditorModels.cs | 310 | process model/editor contract | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.CandidateDiscovery.cs | 593 | process model/editor contract | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs | 299 | process model/editor contract | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs | 643 | process model/editor contract | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs | 460 | AgentFramework model/core usage | No | Observation/runtime service remains owner; out of SB06 dispatcher move |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatModels.cs | 82 | AgentFramework model/core usage | No | Observation/runtime service remains owner; out of SB06 dispatcher move |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs | 389 | execute | No | Observation/runtime service remains owner; out of SB06 dispatcher move |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs | 481 | AgentFramework model/core usage | No | Observation/runtime service remains owner; out of SB06 dispatcher move |
| repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs | 1287 | detail/readback | No | Observation/runtime service remains owner; out of SB06 dispatcher move |
| repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactStatusProjectionService.cs | 69 | finalizer parsing | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs | 1029 | AgentFramework model/core usage | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs | 1694 | AgentFramework model/core usage | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs | 782 | AgentFramework model/core usage | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs | 1085 | finalizer parsing | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEvidenceSourceProvider.cs | 1006 | AgentFramework model/core usage | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs | 955 | AgentFramework model/core usage | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs | 174 | AgentFramework model/core usage | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs | 250 | AgentFramework model/core usage | No | Processes module remains owner for now |
| repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateEditorModelFactory.cs | 173 | process model/editor contract | No | Processes module remains owner for now |
