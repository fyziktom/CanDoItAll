# Source Analysis References

## GPTPro / Existing Bundle Sources

| Source | Used for |
|---|---|
| `codex/bundles/tetris-process-rootcause-workflow-bundle-20260709/02-root-causes.md` | Branch-aware receipt gates, branch-routable completion issues, duplicated receipt contracts, retry-vs-branch routing, domain leakage, template evidence matrix, acceptance criteria matrix. |
| `codex/bundles/tetris-process-rootcause-workflow-bundle-20260709/04-target-architecture.md` | Generic completion gate evaluator, receipt rule resolver, required receipt evaluator, completion issue router, recovery advice provider, evaluation trace, domain boundary rules. |
| `codex/bundles/tetris-process-rootcause-workflow-bundle-20260709/07-domain-boundary-rules.md` | Explicit allowed/forbidden domain knowledge in generic runtime. |
| `codex/bundles/escalation_root_cause_bundle/analysis/02-root-causes.md` | Prompt-only deterministic work, unresolved placeholders, short-circuit completion gates, ignored safe/idempotent diagnostics, generic rework packets, child root-cause loss, file-existence artifact bridge, finalizer semantic gap, name-only preflight, weak template/agent typing. |
| `codex/bundles/escalation_root_cause_bundle/analysis/05-target-architecture.md` | Proposed separations for launch variable resolver, completion gate evaluator, receipt matcher, recovery classifier, recovery instruction builder, subprocess resolver, subprocess artifact bridge, tool-plan executor/guard. |
| `codex/bundles/escalation_root_cause_bundle/analysis/07-why-current-fixes-did-not-solve.md` | Prior fixes improved detection and instructions but did not solve deterministic repair and recovery routing. |
| `codex/bundles/escalation_root_cause_bundle/traceability/02-pro-analysis-closure-matrix.md` | Closure map from GPTPro findings to bundle work. |

## Current Code Sources

| Source | Current concern |
|---|---|
| `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs` | Main adapter still orchestrates subprocesses, agent execution, preflight, runtime-owned .NET setup, output validation, materialization, completion gates, artifact acceptance, and result conversion. |
| `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.*.cs` | Partial-class responsibility expansion across result conversion, product completion, managed artifacts, subprocesses, recovery, grounding, metadata, .NET setup, and acceptance criteria. |
| `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateEvaluator.cs` | Internal evaluator exists, but it is still local to module adapter and uses adapter-specific issue records. |
| `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs` | Internal required receipt gate exists, but it remains adapter/module-local rather than a reusable runtime/driver contract. |
| `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupRuntimeExecutor.cs` | Domain-specific deterministic .NET executor exists in module integration and is injected directly into adapter. |
| `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupToolPlanGuard.cs` | Domain-specific tool-plan guard exists in module integration and uses .NET step/tool knowledge. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptWriter.cs` | Generic receipt writer contains `IsDotNetRuntimeLifecycleTool`, `workspace_dotnet_run`, and `workspace_dotnet_stop` lifecycle logic. |
| `src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions` | Existing driver package/catalog contracts provide a proper extension seam to grow. |

