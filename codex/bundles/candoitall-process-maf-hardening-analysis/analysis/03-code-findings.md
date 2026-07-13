# Code findings with concrete references

## 1. The displayed blocker is an operator projection symptom

`ProcessRuntimeProjectionQueryService.BuildOperatorProblemSummary(...)` appends the exact text “No AgentFramework result summary was found...” when a `StrategyResultReceipt` exists but `diagnostic` is null.

Relevant files:

- `src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs:1400-1426`
- `src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorActionDiagnostics.cs:11-57`

This means the UI has lost the exact agent/result diagnostic. It does not prove that the underlying blocker was tool access. The final hint then falls back to generic capability wording.

## 2. Observation query is run-level, not step-level

`ProcessExecutionObservationQuery` has only `RunIds`, `FromUtc`, `ToUtc`, `TakePerRun`. The reader lists recent execution runs by `ProcessRunId` and applies `TakePerRun` before step grouping.

Relevant files:

- `src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs:19-23`
- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs:53-58`
- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs:117-123`
- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs:197-202`

For large multi-team runs, this can hide the actual execution run for the blocked step. The later dictionary key `(RunId, StepInstanceId)` is too late if the record was truncated already.

## 3. Runtime finalization can diverge from artifact ledger

`SubmitStrategyResult` computes `appliedResult = EnforceStepFinalizationContract(...)`. It uses `appliedResult` for status, diagnostic receipts, produced slots and connected input artifacts. But `BuildArtifactLedgerEvents` receives `command`, and the helper reads `command.Result.ProducedArtifacts`.

Relevant files:

- `src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs:44-55`
- `src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs:86-92`
- `src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs:416-436`

If finalization downgrades `Succeeded` to `NeedsManager`, the artifact ledger should not trust the original result.

## 4. Step contract prompt is not semantically actionable

The runtime contract model is minimal:

- `RequiredArtifactInputRef`: slot ID, availability, producer step ID, artifact ID, content hash, connection hash.
- `ExpectedProducedArtifactRef`: only slot ID.

The prompt builder renders this as GUIDs and hashes.

Relevant files:

- `src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs:62-80`
- `src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeArtifactContracts.cs:118-165`
- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessStepContractPromptBuilder.cs:22-78`

This is good machine state but bad agent guidance. It should include expectation key/title, primary managed ref, accepted child mappings, and required receipts.

## 5. Completed child evidence is too generic

`ResolveCompletedChildEvidenceRefs(...)` collects each existing child step artifact under the child run and falls back to the child steps folder. It does not specifically validate the accepted output mapping from parent template.

Relevant file:

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.SubprocessState.cs:192-221`

This is the likely failure mode for `prepare-solution-skeleton`: product files exist, but the exact parent produced slot has no accepted child handoff proof.

## 6. Readiness checks are not exact composed-tool checks

`AgentProcessReadinessEvaluator` classifies project-structure tool names and checks agent config read/write access. Actual tool execution requires scoped process access in `ProjectStructureAgentRuntimeToolProvider`, including governed process context and allowed operations.

Relevant files:

- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/AgentProcessReadinessEvaluator.cs:354-392`
- `src/Modules/CanDoItAll.Modules.ProjectStructure/ProcessIntegration/ProjectStructureAgentRuntimeToolProvider.cs:2244-2255`
- `src/Modules/CanDoItAll.Modules.ProjectStructure/ProcessIntegration/ProjectStructureAgentRuntimeToolProvider.cs:2271-2297`

The next hardening step is to preflight the exact composed runtime tool list before dispatch.

## 7. `prepare-solution-skeleton` template has a machine/prose mismatch

In `dotnet-development-slice`:

- `prepare-solution-skeleton` is `StepKind: Subprocess`.
- It has `SubprocessProcessKey: dotnet-solution-setup`.
- It has `AllowsManualSkip: true`.
- It expects `solution-skeleton-evidence`.
- Its machine-readable child mapping points to `setup-handoff` only.
- Its markdown accepts both `setup-handoff` and `setup-handoff-after-repair`.

Relevant files:

- `Templates/Processes/processes/dotnet-development-slice/definition.json:269-345`
- `Templates/Processes/processes/dotnet-development-slice/steps/prepare-solution-skeleton.md:3-9`

This should become a typed `SubprocessContract` with accepted child outputs and no-go child outputs.
