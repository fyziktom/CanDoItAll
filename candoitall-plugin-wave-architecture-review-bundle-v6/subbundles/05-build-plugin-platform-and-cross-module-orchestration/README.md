# 05-build-plugin-platform-and-cross-module-orchestration

## Status

- `Prepared for Codex execution`

## Objective

Create a real connector/plugin platform and strengthen cross-module mutation handling before the external integration wave.

## Covered Inputs

- `PW6-005`
- `PW6-008`

## Prerequisites

- SB01 through SB04 complete.
- Decide the minimal manifest contract that every connector plugin must declare.

## Exact Source References

- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workspace/ProviderExecution.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workspace/WorkspaceModuleServiceCollectionExtensions.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs:10-63`
- `src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:26-48`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModuleServiceCollectionExtensions.cs:1-17`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs:1-82; 401-497`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:703-747; 1088-1128`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4684-4749`

## Deliverables

- Plugin/connector manifest and registry.
- Versioned configuration schemas, secret requirements, health/test contract, and capability exposure.
- Durable cross-module orchestration model for multi-entity plugin writes.

## Dependency Impact

- Directly enables email, LinkedIn, and custom API plugins without repeated enum/switch surgery.
- Prevents connector behavior from leaking into Workbench metadata or ad-hoc service switches.

## Validation Depth

- Contract tests for plugin manifests and schema loading.
- At least one skeleton connector plugin proving the new path.
- Failure-path tests for durable recovery / outbox / saga behavior.

## Implementation Steps

- Define the plugin manifest and capability registry.
- Refactor provider/resource handling into first-party plugins that use the same platform.
- Introduce plugin install/enable/disable/test lifecycle and policy surfaces.
- Replace compensation-only multi-module write assumptions with a durable orchestration strategy.

## Do Not Do

- Do not add email or LinkedIn as more enum values plus switch cases.
- Do not let plugins store opaque foreign ids in Workbench metadata.

## Acceptance Checklist

- [ ] A skeleton connector can be added without editing ProviderKind, ResourceKind, or central switch statements.
- [ ] Plugin config is versioned and validated through declared schema.
- [ ] Cross-module failure handling has an explicit durable recovery story.

## Proof Required

- Skeleton plugin implementation.
- Plugin contract tests.
- Recovery-path tests and updated architecture notes.

## Browser Validation Logging

- If plugin configuration UI exists, capture at least one configuration and health-check flow.

## Progression Gate

- Do not start real external plugin work until this subbundle closes.

## Suggested Agent Prompt

Implement SB05 as a real plugin platform. Unify provider/resource/connector registration behind manifests and registries, add durable orchestration for cross-module writes, and do not solve this by expanding enums and switch statements.
