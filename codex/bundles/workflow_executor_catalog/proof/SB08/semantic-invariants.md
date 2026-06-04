# SB08 Semantic Invariants

## Invariant SB08-NO-SILENT-PASS-THROUGH

- Invariant ID: `SB08-NO-SILENT-PASS-THROUGH`
- Source raw note: RN01 and R8 require helper node semantics to be explicit.
- Expected behavior: active helper node kinds without implemented executor semantics fail validation unless intentionally allowed by the graph-only/template validation mode.
- Disallowed shallow implementation: leaving active `Artifact`, `AgentStep`, or `Subworkflow` nodes to reach runtime and pass input through unchanged.
- Failing-first test: N/A - process/non-production exemption because the pass-through risk was addressed by validator semantics and negative validator coverage.
- Passing test: `ValidatorRejectsPlannedExecutorNode` and catalog validator tests in `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`; `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`; `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginWorkflowExecutorDescriptorSource.cs`.
- Production assertions: `bundle://proof/SB10/transcripts/source-assertions-validator-catalog-policy.txt`.
- Red-team negative case: active planned/unknown executor nodes are rejected before runtime dispatch, verified by `ValidatorRejectsPlannedExecutorNode` and `ValidatorRejectsUnknownExecutorId`.
- Downstream dependency check: SB09 UI and templates can display planned/unavailable helper entries without allowing active publish/run.
