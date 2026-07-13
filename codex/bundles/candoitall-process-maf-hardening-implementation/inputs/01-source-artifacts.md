# Source Artifacts

## Preserved Raw Analysis

- `bundle://inputs/gptpro-analysis-source/README.md`
- `bundle://inputs/gptpro-analysis-source/analysis/00-findings-summary.md`
- `bundle://inputs/gptpro-analysis-source/analysis/01-executive-root-cause.md`
- `bundle://inputs/gptpro-analysis-source/analysis/02-architecture-map.md`
- `bundle://inputs/gptpro-analysis-source/analysis/03-code-findings.md`
- `bundle://inputs/gptpro-analysis-source/analysis/04-concrete-incident-prepare-solution-skeleton.md`
- `bundle://inputs/gptpro-analysis-source/analysis/05-template-and-contract-findings.md`
- `bundle://inputs/gptpro-analysis-source/analysis/06-maf-wrapper-and-tool-policy.md`
- `bundle://inputs/gptpro-analysis-source/analysis/07-prioritized-remediation-roadmap.md`
- `bundle://inputs/gptpro-analysis-source/codex/00-CODEX-INSTRUCTIONS.md`
- `bundle://inputs/gptpro-analysis-source/codex/acceptance-criteria.md`
- `bundle://inputs/gptpro-analysis-source/codex/bundles/B01-observability-and-diagnostics.md`
- `bundle://inputs/gptpro-analysis-source/codex/bundles/B02-subprocess-runtime-bridge.md`
- `bundle://inputs/gptpro-analysis-source/codex/bundles/B03-artifact-contract-and-ledger.md`
- `bundle://inputs/gptpro-analysis-source/codex/bundles/B04-capability-tool-preflight.md`
- `bundle://inputs/gptpro-analysis-source/codex/bundles/B05-structured-result-persistence.md`
- `bundle://inputs/gptpro-analysis-source/codex/bundles/B06-template-hardening.md`
- `bundle://inputs/gptpro-analysis-source/codex/bundles/B07-regression-harness.md`
- `bundle://inputs/gptpro-analysis-source/data/findings.json`
- `bundle://inputs/gptpro-analysis-source/data/source-map.csv`
- `bundle://inputs/gptpro-analysis-source/evidence/calculator-output-inspection.md`
- `bundle://inputs/gptpro-analysis-source/evidence/static-analysis-notes.md`
- `bundle://inputs/gptpro-analysis-source/checklists/hardening-checklist.csv`
- `bundle://inputs/gptpro-analysis-source/mermaid/current-flow.mmd`
- `bundle://inputs/gptpro-analysis-source/mermaid/target-flow.mmd`

The original pack remains available at `repo://codex/bundles/candoitall-process-maf-hardening-analysis`.

## Repository Sources Inspected During Preparation

- `repo://Templates/Processes/processes`
- `repo://Templates/Processes/shared/artifacts`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorActionDiagnostics.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.SubprocessState.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessStepContractPromptBuilder.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeArtifactContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateStepSummaries.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/AgentProcessReadinessEvaluator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`

## CodeAnalytics Evidence

- Snapshot id: `snap-20260708104406-98263759`
- Scope: `CanDoItAll.Processes.*`, `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.ProjectStructure` requested but provider file currently lives in `CanDoItAll.Modules.Workbench`, and relevant `CanDoItAll.AgentFramework.*` projects.
- Project count: 16 scoped source projects.
- Document count: 427.
- Dependency cycles: `[]`.
- Diagnostics: diagram truncation for large projects; unrelated advisory warnings for `Microsoft.OpenApi` in app/test/tool projects.
