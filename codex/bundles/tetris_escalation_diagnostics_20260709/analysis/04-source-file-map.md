# Source File Map

This folder includes selected source snapshots under `source-context/repo-files`. The user will provide the whole source tree too, but these are the most relevant files for this escalation.

## Runtime Adapter And Completion Gates

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs`
  - Validates required product paths and file content checks.
  - Produces diagnostics like `process.adapter.product_required_file_content_missing`.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionParsing.cs`
  - Parses `ProductCompletionRequiredFileContentChecksByStep` and branch outcome enforcement keys.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Types.cs`
  - Defines content-check records and completion issue models.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeStepAssignmentRepairService.cs`
  - Rebuilds/retries step assignments after diagnostics.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessLaunchExecutorResolver.cs`
  - Resolves process launch executor strategy and variables.

## Recovery Guidance

- `src/Processes/CanDoItAll.Processes.Application/ProcessStepRecoveryInstructionBuilder.cs`
  - Contains diagnostic-specific recovery guidance for ungrounded refs, QA content/readback failures, and missing receipts.
  - Earlier repairs added QA branch behavior: product content/readback failure should use `repair-required` for `qa-validation` and `repair-escalation` for `qa-recheck`.

## Launch Variables And Required Gates

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
  - Emits required .NET validation receipts and visible UI scaffold-removal checks.
  - Earlier repair added ungated scaffold-removal checks for `quality-repair` and branch-gated checks for QA acceptance.

- `src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
  - Filters launch-variable maps by step and injects step-specific required receipt/check data.

- `src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeLaunchVariables.cs`
  - Defines launch-variable names used by the adapter.

## Templates

- `Templates/Processes/processes/software-delivery/definition.json`
- `Templates/Processes/processes/software-delivery/steps/qa-validation.md`
- `Templates/Processes/processes/software-delivery/steps/qa-recheck.md`
- `Templates/Processes/processes/software-delivery/steps/quality-repair.md`
- `Templates/Processes/processes/software-delivery/steps/peer-review.md`

## Tests

- `tests/Unit/CanDoItAll.Tests.Unit/DotNetProcessLaunchVariableContributorTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProcessStepRecoveryInstructionBuilderTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs`

## Prior Pro Root-Cause Source

Copied under `source-context/prior-pro-root-cause`. Start with:
- `analysis/02-root-causes.md`
- `codex/04-diagnostic-specific-rework-packets.md`
- `codex/03-safe-auto-rework-recovery.md`
