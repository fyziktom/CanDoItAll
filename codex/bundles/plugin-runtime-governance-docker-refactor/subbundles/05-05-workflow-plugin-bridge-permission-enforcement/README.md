# SB05 Workflow Plugin Bridge Permission Enforcement

## Status

- `Completed`

## Objective

- Complete the plugin workflow executor bridge so workflow validation, workflow editor availability, and runtime execution all enforce plugin installation, enabled state, connection state, and explicit grants.

## Success Criteria

- Plugin executor nodes are unavailable before execution when required grants or connections are missing.
- Runtime rechecks grants before invocation.
- Workflow editor surfaces actionable diagnostics for plugin permission problems.

## Covered Inputs

- `N007`: workflows must support Docker logs followed by LLM summary.
- `N008`: plugin execution remains generic.
- `N009`: tool access must be explicitly allowed.
- Requirements `R012`, `R013`, `R014`, `R015`, and `R019`.

## Prerequisites

- SB02 grant evaluator is complete.
- SB04 settings UI/API persists grants and connections.
- SB03 host-tool denied behavior is available for runtime capability context.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorObservability.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutorDescriptors.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginManifestTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginWaveArchitectureGuardrailTests.cs

## Deliverables

- Plugin workflow executor catalog bridge that publishes descriptors only with accurate availability.
- Runtime plugin executor adapter that builds grant-aware capability context per invocation.
- Workflow validation rules for disabled plugin, unavailable plugin, missing connection, missing grant, revoked grant, and invalid policy.
- Workflow editor diagnostics for missing plugin grants and connections.
- Audit records that include plugin id, connection id, grant/recipe decision, redacted settings, and payload/truncation metadata.

## Dependency Impact

- SB06 Docker workflow proof depends on generic plugin executor bridge behavior.
- SB07 performance review depends on how grant checks are loaded during validation and execution.
- SB08 architecture review depends on workflow validation and runtime using the same policy.

## Validation Depth

- `Critical runtime foundation`

## Implementation Steps

1. Add plugin executor descriptor bridge from installed/enabled plugin catalog state.
2. Add availability diagnostics for unavailable plugin executor descriptors.
3. Build plugin execution context through SB02 capability proxy factory and SB03 host-tool capability.
4. Add workflow validation checks for plugin grants and connections.
5. Add runtime recheck immediately before plugin invocation.
6. Add tests for validation-time and runtime missing-grant cases.
7. Add workflow editor browser proof for missing-grant diagnostics.
8. Update execution report with commands and browser artifacts.

## Scope Exceptions

- No Docker plugin implementation in this subbundle.
- No new LLM executor implementation unless required by an existing workflow bridge gap.

## Do Not Do

- Do not execute plugin nodes based only on descriptor availability.
- Do not trust validation results without runtime recheck.
- Do not pass raw settings or secrets into audit records.
- Do not build Docker-specific workflow logic into the bridge.

## Acceptance Checklist

- Disabled plugin executor cannot run.
- Missing connection executor cannot run.
- Missing grant executor cannot run.
- Runtime and validation share the same policy decision source.
- Workflow editor displays actionable missing-grant diagnostics.
- Plugin payload and LLM input limits are enforced or clearly delegated to existing policy.

## Proof Required

- Unit/integration test command and result for plugin workflow validation.
- Runtime test command and result for missing grant after validation.
- Browser screenshot showing workflow editor missing-grant diagnostic.
- Execution report row updated with SB05 closure decision and browser analytics.

## Browser Validation Logging

- Route: concrete workflow editor route used by the application.
- Viewports: large-screen pass; narrower-width pass if diagnostics affect layout.
- Playwright actions: create or open workflow with plugin executor node, assert disabled/missing-grant warning, grant permission if route supports it, assert warning changes.
- Screenshots: missing-grant warning and resolved/changed state when feasible.
- Review questions: diagnostics must name the missing permission without exposing secret data, and node controls must not imply execution is allowed.

## Progression Gate

- SB06 may start only when workflow validation and runtime execution both reject plugin executor nodes with missing grants.

## Suggested Agent Prompt

```text
Implement SB05 only.
Complete the generic plugin workflow bridge and permission enforcement using the SB02 grant evaluator and SB04 permission state. Capture workflow editor browser proof. Do not implement Docker plugin behavior.
```
