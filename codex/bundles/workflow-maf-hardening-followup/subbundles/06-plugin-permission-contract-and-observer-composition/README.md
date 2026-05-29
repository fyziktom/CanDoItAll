# 06-plugin-permission-contract-and-observer-composition

## Status

- Status: `Completed`

## Objective

Make plugin workflow executor governance deterministic and internally consistent.

## Covered Inputs

- R7: Make plugin observer registration deterministic and order-independent.
- R8: Validate plugin executor permission policy against plugin manifest capabilities and connection metadata.
- R3: Keep approval-required executor semantics aligned with product approval gates.
- R11: Keep live external effects disabled by default in proof.

## Prerequisites

- SB05 payload policy is completed or blocked with explicit plugin-log impact.
- SB02 approval semantics are available for external write governance.

## Exact Source References

- `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs`
- `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs`
- `repo://src/CanDoItAll.Modules.Plugins/Services/PluginsModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Docker/DockerWorkflowExecutors.cs`

## Scope

- Replace order-dependent single observer registration with deterministic composition.
- Extend plugin manifest validation for permission/capability consistency.
- Add fake-mode tests for bundled Gmail, Office365, and Docker workflow executors.
- Keep Docker host-command governance strict.

## Dependency Impact

- SB07 backend/runtime honesty must report plugin executor availability without hiding governance failures.
- SB08 final regression depends on fake-mode proof and order-independent observer behavior.

## Validation Depth

- DI composition tests in both module orders, manifest validation tests, and fake-mode plugin tests.
- Critical proof requires negative permission mismatch cases and no-live-effect assertions.

## Implementation Steps

1. Introduce observer sink/composite or equivalent `IEnumerable<>` composition.
2. Add DI tests for AgentFramework then Plugins and Plugins then AgentFramework registration orders.
3. Extend `PluginManifestValidator` for host command, secrets, network, external writes, and deterministic test-mode consistency.
4. Consolidate bundled plugin workflow executor descriptor construction where practical.
5. Add fake-mode integration tests for Gmail, Office365, and Docker.
6. Validate Docker arguments against recipe allowlists and avoid arbitrary host shell commands.

## Do Not Do

- Do not execute live external calls in default tests.
- Do not make plugin observers optional by registration order.
- Do not hide permission/capability mismatches as warnings only.

## Acceptance Checklist

- Plugin audit records are persisted regardless of module registration order.
- Manifest validation fails inconsistent plugin permission policies.
- Gmail, Office365, and Docker fake-mode tests pass without secrets.
- Host-command governance remains strict.

## Proof Required

- DI composition tests.
- Manifest validation tests.
- Plugin fake-mode tests.
- `bundle://proof/SB06/manifest.md` and `bundle://proof/SB06/semantic-invariants.md`.

## Browser Validation Logging

- Browser proof is not required unless plugin executor UI surfaces change.

## Progression Gate

- Continue to SB07 only after plugin execution governance is deterministic and default proof does not touch live external systems.

## Suggested Agent Prompt

Implement deterministic plugin observer composition, strict manifest permission validation, and fake-mode plugin proof without live external side effects.
