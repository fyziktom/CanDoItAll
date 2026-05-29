# SB06 proof manifest

Status: Completed

## Summary

- Added deterministic workflow executor audit composition through `IWorkflowExecutorExecutionAuditSink` and `CompositeWorkflowExecutorExecutionObserver`.
- Registered plugin audit logging as an audit sink so module registration order no longer decides whether plugin executor audit records are persisted.
- Extended plugin manifest validation for workflow executor permission/capability consistency, connection metadata, external-write approval, host-command approval, and deterministic test-mode declarations.
- Added fake-mode proof for bundled Gmail, Office365, and Docker workflow executors without invoking live external services.
- No UI surface changed; browser proof is not required for this subbundle.

## Source Changes

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorObservability.cs`
- `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Plugins/Services/PluginsModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`
- `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentFrameworkHostingServiceCollectionTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/PluginManifestTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/PluginCatalogIntegrationTests.cs`

Hash sample: `3910f219b5cabda347b899a18d8a9ab28f20c3899c7ab644407895f0ca09b376`.

## Proof

- `bundle://proof/SB06/transcripts/failing-first-plugin-governance-tests.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~PluginCatalogIntegrationTests.Workflow_executor_observer_registration_composes_plugin_sink_regardless_module_order|FullyQualifiedName~PluginCatalogIntegrationTests.Bundled_plugin_preview_simulation_avoids_live_external_effects"`
  - Result: failed before implementation because `IWorkflowExecutorExecutionAuditSink` and `CompositeWorkflowExecutorExecutionObserver` did not exist.
- `bundle://proof/SB06/transcripts/failing-first-plugin-manifest-validation-tests.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~PluginManifestTests.PluginManifest_validator_rejects_executor_permission_policy_without_manifest_capabilities|FullyQualifiedName~PluginManifestTests.PluginManifest_validator_rejects_external_write_without_approval_and_deterministic_mismatch"`
  - Result: failed before implementation because `MissingConnectionMetadata` and `InconsistentPermissionPolicy` validation issues did not exist.
- `bundle://proof/SB06/transcripts/unit-plugin-manifest-validation-after-implementation.txt`
  - Command: same targeted manifest validation filter.
  - Result: 2 passed, 0 failed, 0 skipped.
- `bundle://proof/SB06/transcripts/unit-plugin-manifest-class-after-implementation.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~PluginManifestTests"`
  - Result: 8 passed, 0 failed, 0 skipped.
- `bundle://proof/SB06/transcripts/unit-agent-framework-hosting-di-after-implementation.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentFrameworkHostingServiceCollectionTests.AddAgentFrameworkCore_builds_with_scope_validation"`
  - Result: 1 passed, 0 failed, 0 skipped.
- `bundle://proof/SB06/transcripts/integration-plugin-governance-after-implementation.txt`
  - Command: targeted DI/fake-mode integration filter.
  - Result: 7 passed, 0 failed, 0 skipped.
- `bundle://proof/SB06/transcripts/integration-plugin-log-regression-after-observer-composition.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~PluginCatalogIntegrationTests.Plugin_logs_persist_installation_runtime_and_redact_sensitive_values"`
  - Result: 1 passed, 0 failed, 0 skipped.
- `bundle://proof/SB06/transcripts/integration-plugin-catalog-class-after-implementation.txt`
  - Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~PluginCatalogIntegrationTests"`
  - Result: 27 passed, 0 failed, 0 skipped.
- `bundle://proof/SB06/transcripts/source-assertions-plugin-governance.txt`
  - Command: `rg -n "IWorkflowExecutorExecutionAuditSink|CompositeWorkflowExecutorExecutionObserver|PluginWorkflowExecutorExecutionObserver|MissingConnectionMetadata|InconsistentPermissionPolicy|RunsHostCommand|WritesExternalData|SupportsDeterministicTestMode|AddPluginsModule|AddAgentFrameworkModule" src tests`
  - Result: source assertions found observer composition, plugin sink registration, manifest validation issues, strict permission flags, and tests.
- `bundle://proof/SB06/transcripts/anti-stub-audit-plugin-governance.txt`
  - Command: narrow `rg` scan for `TODO`, `NotImplementedException`, `fixture-specific`, and `template-only` in SB06 touchpoints.
  - Result: no anti-stub markers found.
- `bundle://proof/SB06/transcripts/build-after-sb06.txt`
  - Command: `dotnet build CanDoItAll.slnx --no-restore`
  - Result: build passed with 0 errors and existing EF Core Relational assembly-version warnings.
- `bundle://proof/SB06/transcripts/git-diff-check-after-sb06.txt`
  - Command: `git diff --check`
  - Result: passed with line-ending normalization warnings only.
- `bundle://proof/SB06/transcripts/bundle-validator-prepared-after-sb06.txt`
  - Command: `python bundle-preparation validate_bundle.py codex\bundles\workflow-maf-hardening-followup --stage prepared`
  - Result: bundle is valid for stage `prepared`.
- Passing transcript: `bundle://proof/SB06/transcripts/unit-plugin-manifest-validation-after-implementation.txt`
- Anti-stub transcript: `bundle://proof/SB06/transcripts/anti-stub-audit-plugin-governance.txt`
- `bundle://proof/SB06/transcripts/semantic-invariant-evidence.txt`
  - Command: semantic invariant transcript index.
  - Result: invariant ids are indexed for completed-stage validation.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Composite executor observer | agent framework DI | workflow executor invoker and plugin log sink | Built from registered audit sinks in deterministic order. | `bundle://proof/SB06/transcripts/failing-first-plugin-governance-tests.txt`; `bundle://proof/SB06/transcripts/integration-plugin-governance-after-implementation.txt` |
| Plugin manifest validation issues | plugin manifest validator | plugin catalog/install validation | Produced before plugin workflow executors can be exposed. | `bundle://proof/SB06/transcripts/failing-first-plugin-manifest-validation-tests.txt`; `bundle://proof/SB06/transcripts/unit-plugin-manifest-validation-after-implementation.txt` |

## Skipped

- Live Gmail, Office365, Docker, and host-command workflow proof was not run per bundle boundary.
- Browser proof was not run because SB06 did not change UI.
