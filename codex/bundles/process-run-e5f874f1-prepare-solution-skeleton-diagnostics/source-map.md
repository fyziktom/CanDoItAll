# Source Map

This file maps the captured runtime evidence to source files Pro should inspect.

## Subprocess Parent Path

- `src/Processes/CanDoItAll.Processes.Application/ProcessBlockedStepPacket.cs:101`
  Builds the UI text used when a process runtime receipt exists but no exact AgentFramework result summary exists. This explains the parent `prepare-solution-skeleton` message.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs:84`
  Computes the subprocess launch/deferred outcome hash from `ParentDeferredOutcomeJson`. This is where subprocess launch/propagation enters the strategy result path.

- `src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs:291`
  Documents the child-outcome rule: stopped children propagate their concrete blocker; blocked children must not be blindly relaunched.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs:413`
  Adds the same subprocess child-outcome rule to the agent-facing process step brief.

## Child Setup Contract

- `Templates/Processes/processes/dotnet-solution-setup/steps/create-dotnet-project.md:11`
  The step instructions require running the helper with `workspace_pwsh_run_script`.

- `Templates/Processes/processes/dotnet-solution-setup/steps/create-dotnet-project.md:13`
  The primary step artifact and `Completed` outcome are allowed only after helper execution and readback.

- `Templates/Processes/processes/dotnet-solution-setup/steps/create-dotnet-project.md:47`
  The file-content check is a hard gate and specifically requires the solution file to contain the app project path.

- `Templates/Processes/processes/dotnet-solution-setup/definition.json:173`
  The create step notes repeat the same contract: scaffold solution/app, run `DotNetCreateProjectScript`, read back files, and do not complete before required paths, tool receipts, helper receipt, and solution membership pass.

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs:156`
  Adds step-scoped file-content checks for solution setup.

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs:169`
  Adds `DotNetCreateProjectScriptRef`.

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs:172`
  Adds `DotNetCreateProjectScript`.

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs:470`
  Builds the launch-variable guidance explaining that `create-dotnet-project` must leave the solution containing the app project.

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs:473`
  States the product file-content checks are hard readback gates.

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs:524`
  Requires `template=sln`, the app template receipt, and `workspace_pwsh_run_script` for `create-dotnet-project`.

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs:831`
  Builds the deterministic execution plan. The captured agent run stopped after dotnet new and artifact write, before the helper and solution readback.

## Runtime Validation Path

- `src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs:1209`
  Resolves step-scoped `ProductCompletionRequiredToolReceipts`.

- `src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs:1224`
  Resolves step-scoped `ProductCompletionRequiredFileContentChecks`.

- `src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs:189`
  Parses `ProductCompletionRequiredToolReceipts`.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs:168`
  Validates required product tool receipts on a succeeded agent outcome.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs:174`
  Validates required process tool receipts on a succeeded agent outcome.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs:61`
  Implements `ValidateRequiredProductToolReceipts`. Pro should verify why it did not produce `process.adapter.product_required_tool_receipt_missing` for the missing `workspace_pwsh_run_script` in this run.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs:89`
  The expected diagnostic code for missing required product tool receipts.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs:249`
  Contains special guidance for missing `workspace_pwsh_run_script`.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs:138`
  Implements `ValidateRequiredProductFileContentChecks`.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs:201`
  Adds the exact failure used in this run when expected text is absent from the solution file.

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionPaths.cs:230`
  Emits `process.adapter.product_required_file_content_missing`, the diagnostic captured in `db/child-create-receipt.txt`.

## Product Evidence

- `product-target/Calculator.slnx.txt`
  Captured solution file content. It is only `<Solution></Solution>`.

- `product-target/dotnet-slnx-list.txt`
  Captured `dotnet sln Calculator.slnx list` output. It reports no projects in the solution.

- `workspace-artifacts/ab4a1ed8-8b1b-4974-973d-93983bf41f09/steps/create-dotnet-project.md`
  The child agent wrote this managed artifact, but the runtime receipt did not accept it as a produced process artifact because the product completion gate failed.
