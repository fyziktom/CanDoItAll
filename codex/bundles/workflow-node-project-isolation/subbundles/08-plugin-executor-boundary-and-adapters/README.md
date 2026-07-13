# SB08 - Plugin Executor Boundary And Adapters

## Status

- `Completed`

## Objective

Move plugin-provided workflow executor compatibility into explicit executor/plugin boundary projects so plugin manifests, package loading, runtime registration, trust/source metadata, grants, side effects, OAuth/secrets, deterministic preview, and bundled plugin executors remain first-class after executor isolation.

## Success Criteria

- Plugin executor descriptors project through executor abstractions without depending on MAF-owned helper code.
- Runtime package executors still load, register, report source/trust metadata, and execute through the same executor catalog.
- Bundled Docker, Gmail, and Office365 executors keep grants, approval behavior, deterministic preview, side-effect receipts, and sensitive-data handling.
- Plugin load, package dependency, DI activation, grant, OAuth, host-tool, external provider, and plugin execution failures produce typed diagnostics with plugin/package/type/operation context.
- Plugin manifest contracts remain compatible for installed packages.

## Covered Inputs

- R07, R09, R13, R14, R15, R17.
- Architect note that plugins are a major source of executors and consequences must be analyzed deeply.

## Prerequisites

- SB06 completed.
- SB07 can proceed in parallel for default executors, but SB09 must wait for both SB07 and SB08.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginManifestContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\PluginWorkflowExecutorDescriptorSource.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\PluginWorkflowExecutorRuntimeRegistration.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\RuntimePackageWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\RuntimePackageWorkflowExecutorDescriptorSource.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\IPluginWorkflowExecutorGrantEvaluator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Services\PluginsModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs`

## Deliverables

- `CanDoItAll.AgentFramework.WorkflowExecutors.Plugins` for plugin descriptor projection, runtime package executor adapter, plugin executor registration bridge, and plugin-specific validation helpers.
- Compatibility updates to `CanDoItAll.Plugins.Abstractions` only when necessary and backward-compatible.
- Tests for manifest descriptor projection, package assembly executor discovery, source/trust metadata, grant availability, OAuth/secrets masking, host-tool approval, side-effect receipts, deterministic preview, plugin diagnostics, and plugin audit sink behavior.
- Migration notes for installed plugin package compatibility.

## Dependency Impact

- SB09 hardening and SB10 template descriptor validation depend on plugin executor parity. SB12 UI/plugin display adoption depends on descriptor source metadata staying stable. Weak proof here can break external side effects and installed plugin packages.

## Validation Depth

- `Critical plugin executor compatibility`
- Unit, integration, manifest compatibility, security/secret masking, side-effect, and package-loading proof.

## Implementation Steps

1. Move plugin executor descriptor projection and runtime package adapter logic into the plugin executor boundary project.
2. Keep plugin manifest public contracts stable; if a contract must change, add compatibility adapters and tests.
3. Verify `IPluginWorkflowExecutor` and plugin service registry still bridge into the executor catalog.
4. Add tests for descriptor source/trust, grants, permission policy, side effects, deterministic test mode, diagnostics, and audit events.
5. Add bundled plugin tests for Docker host-tool approval/failure, Gmail OAuth/idempotency receipt handling/failure, and Office365 Graph side-effect receipt/failure behavior.
6. Verify package loading still discovers workflow executor types assignable to the correct abstraction.
7. Add negative tests for missing package dependency, plugin DI activation failure, executor throw, missing grant, missing/expired OAuth, missing secret, rate limit, external service unavailable, and secret redaction.
8. Update inventories, workbook, and proof.

## Scope Exceptions

- Creating new external plugin features is out of scope.
- UI rendering of plugin executor cards is SB12.
- Broad plugin marketplace/package management changes are out of scope unless required for workflow executor compatibility.

## Do Not Do

- Do not break existing plugin manifest schema without a tested migration path.
- Do not log secrets, OAuth tokens, email addresses beyond existing masked policy, file contents, or host command arguments without masking.
- Do not bypass grant, trust, approval, or side-effect receipt logic to make tests pass.
- Do not keep a MAF-only adapter as a hidden fallback path.
- Do not collapse plugin package load, activation, grant, OAuth, provider, or execution failures into one generic plugin error.

## Acceptance Checklist

- [x] Plugin executor descriptor source works through executor abstractions.
- [x] Runtime package executor discovery and registration pass tests.
- [x] Bundled Docker/Gmail/Office365 executor behavior is covered.
- [x] Grants, trust/source, side effects, OAuth/secrets masking, and deterministic preview are preserved.
- [x] Plugin failure diagnostics include plugin id, package id, executor id, type name, operation/provider/tool context, retryability, repair hint, and redacted technical detail when known.
- [x] Plugin compatibility risks are documented in execution report.

## Execution Notes

- Added `CanDoItAll.AgentFramework.WorkflowExecutors.Plugins` as the plugin executor boundary project.
- Moved plugin workflow executor descriptor projection out of `CanDoItAll.Modules.Plugins`.
- Added `IPluginWorkflowExecutorGrantEvaluator` so the boundary consumes strongly typed grant decisions without referencing module persistence or EF services.
- Kept `PluginGrantEvaluator` in `CanDoItAll.Modules.Plugins` and implemented the boundary grant evaluator interface there.
- Moved runtime package executor wrapping and descriptor-source registration into `PluginWorkflowExecutorRuntimeRegistration`, `RuntimePackageWorkflowExecutor`, and `RuntimePackageWorkflowExecutorDescriptorSource`.
- Kept package manifest storage, package installation, load-context resolution, hosted restart state, OAuth services, audit sink, and plugin UI surfaces in the plugin module.
- Updated `PluginsModuleServiceCollectionExtensions` to register the boundary through `AddPluginWorkflowExecutorBoundary()` and to bridge `PluginGrantEvaluator` into the boundary interface.
- Updated the workbook Source Map, Plugin Consequences, Subbundles, Validation Matrix, and Summary rows for SB08.

## Validation Notes

- Plugin executor boundary project build passed with 0 warnings and 0 errors.
- `CanDoItAll.Modules.Plugins` build passed with 0 warnings and 0 errors.
- Bundled Docker, Gmail, Office365, and Email plugin builds passed with 0 warnings and 0 errors.
- New `PluginWorkflowExecutorBoundaryTests` passed: `5/5`.
- Existing plugin manifest/capability/executor-policy regression slice passed: `39/39`.
- Plugin catalog and email plugin integration slice passed: `48/48` from an alternate output path because the default Web bin output is locked by an already-running `CanDoItAll.Web` process.
- Static ownership scans found no `Modules.Plugins`, MAF, Web, EF, or Infrastructure references from `WorkflowExecutors.Plugins`.
- Anti-stub scan found no placeholder markers in plugin boundary source or SB08 tests.

## Proof Required

- `proof/SB08/manifest.md` with changed file hashes, package loading test transcripts, bundled plugin transcripts, and compatibility notes.
- `proof/SB08/semantic-invariants.md` covering manifest compatibility, trust/source metadata, grant checks, secret masking, side-effect receipts, deterministic preview, typed plugin failures, retryability, repair hints, and no MAF fallback.
- Semantic Adequacy Gate proof with adversarial untrusted/missing grant/secret leakage cases, positive bundled plugin execution or deterministic preview cases, and anti-stub audit.

## Browser Validation Logging

- `N/A` for this subbundle. Browser-visible plugin display proof is SB12.

## Progression Gate

- SB09 cannot start until plugin executor compatibility passes and all plugin consequences in the workbook have an owner, proof, or explicit risk decision.

## Suggested Agent Prompt

```text
Implement SB08 only. Isolate plugin workflow executor boundaries and adapters after SB06. Preserve manifest compatibility, runtime package discovery, grants, source/trust metadata, side-effect receipts, deterministic preview, diagnostics, and secret masking. Add bundled plugin, negative failure, and package-loading proof. Do not perform UI adoption or template migration.
```
