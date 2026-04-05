# 04-plugin-platform-and-cross-module-seams

## Status

- `Prepared for Codex execution`

## Objective

Create a real plugin/connector platform and harden cross-module write boundaries before email, LinkedIn, and custom API integrations arrive.

## Covered Inputs

- `PWA-006`
- `PWA-007`
- `R-001`
- `R-002`
- `R-005`

## Prerequisites

- SB01 through SB03 complete.
- Carrier/facet and kind-registry boundaries stable.

## Exact Source References

- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workspace/WorkspaceModuleServiceCollectionExtensions.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workspace/ProviderExecution.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs:10-15`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModuleServiceCollectionExtensions.cs:1-18`
- `src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:26-44`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs:1-90`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs:401-497`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:662-749`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:989-1135`
- `src/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs:121-198`

## Deliverables

- Plugin/connector manifest contract with capability, schema, secret, health, and policy metadata.
- Unified connector registry used by Workspace, Resources, Workbench, and agent layers.
- Explicit cross-module mutation strategy: transaction where possible or outbox/saga orchestration where not.

## Dependency Impact

- This is the direct foundation for email, LinkedIn, and custom API plugins.
- It prevents plugin-specific logic from leaking into Workbench metadata or provider/resource enums.

## Validation Depth

- Contract tests for plugin manifests and schema loading.
- Permission/policy tests for agent exposure.
- Failure-path tests for cross-module mutation recovery.
- At least one skeleton plugin proving the new platform path.

## Implementation Steps

- Define IConnectorPlugin (or equivalent) with descriptor/manifest, config schema, secret requirements, health check, agent capability map, and node-kind hooks.
- Refactor provider/resource handling to registry-driven plugin resolution.
- Introduce plugin installation/enable/disable/test lifecycle.
- Replace compensation-only cross-module write flows with a stronger orchestration boundary before more modules adopt the pattern.

## Do Not Do

- Do not add email or LinkedIn as another enum value plus switch block.
- Do not let plugins write opaque foreign ids into Workbench metadata as a shortcut.

## Acceptance Checklist

- [ ] A skeleton connector plugin can be added without modifying existing provider/resource enums.
- [ ] Plugin configuration is versioned and validated through a declared schema.
- [ ] Cross-module failure handling has an explicit durable recovery story.

## Proof Required

- Skeleton plugin implementation.
- Contract tests.
- Recovery-path tests and architecture notes.

## Browser Validation Logging

- If plugin configuration UI exists, capture at least one configuration and health-check flow.

## Progression Gate

- Do not start real external plugin work until this subbundle closes.

## Suggested Agent Prompt

Implement SB04 as a real plugin platform, not as more enums and switch statements. Unify connector/provider/resource registration behind manifests and registries, define permission and health contracts, and strengthen cross-module mutation handling before any new integration ships.
