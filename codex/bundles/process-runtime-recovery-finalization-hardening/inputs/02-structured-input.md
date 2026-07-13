# Structured Input

## Objectives

- Make process-step readiness, finalization, and manager handoff explicit enough that a step cannot complete from partial work or lost context.
- Replace broad automatic retry with typed recovery routing: current-step retry, upstream-step repair, manager access grant or reassignment, wait for external input, or terminal block.
- Preserve connected artifact semantics across arbitrary process graph edges, including artifacts produced by non-direct prior steps and subprocess boundaries.
- Give agents and finalizers fresh, tool-backed access to step contract, required inputs, expected outputs, artifact refs, tool receipt requirements, and manager handoff expectations.
- Reduce context overload by passing artifact manifests and retrieval handles, not full code/file dumps, unless a process-driver contract explicitly requires inline content.
- Isolate generic runtime contracts from AgentFramework, MAF, project-structure, .NET delivery, browser proof, and other domain-specific process behavior.
- Break up partial-class responsibility clusters only through real boundaries with independent tests.

## Hard Constraints

- Runtime, builder, core, persistence, projections, and dispatcher contracts must remain generic for enterprise processes.
- Domain-specific process behavior belongs in templates, drivers, process-module integration, or domain contributors.
- Do not add a final design that grows `ProcessRuntimeEngine` partial files or `AgentFrameworkProcessExecutionAdapter` partial files.
- Do not silently hide missing artifacts, missing tools, denied access, or missing manager decisions behind retry.
- Do not treat a slot id alone as sufficient proof that the right connected artifact is available to a downstream step.
- Do not pass all changed product files as downstream artifacts by default.

## Architecture Signals From Repo Inspection

- `ProcessRuntimeScheduler` checks dependencies and required artifact slots before dispatching pending steps, but `ProcessRuntimeStateSnapshot.AvailableArtifactSlots` only records slot availability and not the concrete connected artifact refs that the next step should consume.
- `ProcessTemplateKernelBuilder.ResolveRequiredSlots` uses `(SourceStepKey, ArtifactExpectationKey)`, so non-direct upstream artifact connections can be represented at launch time.
- `ProcessRuntimeEngine.ResultHelpers` maps adapter `NeedsManager` results with safe/idempotent `process.adapter.*` diagnostics back to `ProcessRuntimeStepStatus.Ready`, which makes manager signals double as automatic retry signals.
- `AgentFrameworkProcessExecutionAdapter` already validates many completion issues and required tool receipts, but many missing-output and receipt cases are marked `SafeToRetry`.
- `AgentFrameworkProcessStepBriefBuilder` contains strong prompt instructions for reading upstream artifacts and finalizing managed artifacts, but those instructions are not enough when agent context is compressed or lost.
- CodeAnalytics flagged large process/adapter files, including `ProcessRuntimeDispatchApplicationService.cs`, `ProcessRuntimeProjectionQueryService.cs`, `ProcessLaunchApplicationService.cs`, `AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`, `ProcessRuntimeEvidenceSourceProvider.cs`, and the runtime engine partial files.

## Assumption

This bundle prepares a larger architecture program. Execution may split implementation over multiple turns or agents, but every phase must preserve the prepared dependency gates and proof rules.
