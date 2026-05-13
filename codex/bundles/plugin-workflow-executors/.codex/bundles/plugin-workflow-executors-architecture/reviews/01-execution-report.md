# Execution Report

## How To Use

Codex must update this file after every subbundle. Do not rely on chat history. Record commands, screenshots, changed files, skipped scope, and gate decisions.

## Status

- Current subbundle: `SB11`
- Last completed subbundle: `SB10`
- Current gate: `Before SB11`
- Open blockers: `None recorded`
- Last architecture review result: `N/A`

## Subbundle Status

| Id | Folder | Status | Proof Summary | Open Issues |
| --- | --- | --- | --- | --- |
| SB01 | 01-01-plugin-readiness-source-audit-and-decision-gate | Completed | Prepared validator passed after metadata-only heading repair. All exact source references resolved. Live-source audit reconfirmed descriptor metadata, JSON-only settings validation, hard-coded workflow settings UI, non-consumer-bound secret resolver, and project-structure service-provider lookup still match bundle assumptions. | None |
| SB02 | 02-02-workflow-executor-contract-hardening | Completed | Added workflow executor source/trust/availability/settings-schema metadata, built-in/planned descriptor metadata, `CanExecute` runtime semantics, validator/invoker rejection for non-runnable executors, duplicate implementation guard, and focused unit tests for metadata, legacy JSON compatibility, planned executors, and duplicate implementations. | None |
| SB03 | 03-03-settings-schema-canonicalization-and-validator | Completed | Added shared canonical configuration schema/state/validator, adapted connector schema/state compatibility, generated workflow executor configuration schemas, validated executor settings via `WorkflowDefinitionValidator`, kept enum numeric JSON compatibility through explicit select aliases, and proved connector field rendering accepts canonical descriptors. | None |
| SB04 | 04-04-settings-renderer-registry-and-schema-fallback | Completed | Added a trusted settings renderer registry, schema fallback component, DynamicComponent renderer host, DI registration, workflow canvas integration, canonical state round-tripping for executor settings, and focused unit/component proof. Browser proof verified a Workspace files executor node renders schema-backed fallback fields in desktop and narrow layouts. | None |
| SB05 | 05-05-secret-runtime-authorization-and-plugin-secret-broker | Completed | Added plugin strict consumer-bound secret authorization, typed runtime consumer constants/ids, persisted secret binding request/summary APIs, `IPluginSecretBroker`, DI registration, runtime provider/storage resolver paths, and tests for bound, wrong-consumer, wrong-purpose, deleted, integration DI, and redacted summaries. | None |
| SB06 | 06-06-workspace-file-storage-project-facades | Completed | Added constrained `IPluginWorkspaceFiles`, safe `IPluginStorageGateway`, stable `IProjectStructureRuntimeGateway` contracts, a Workbench gateway adapter, fallback unavailable gateway registrations, and refactored `ProjectStructureWorkflowExecutor` away from `IServiceScopeFactory`/`ProjectStructureAgentService` lookup. Unit proof covers relative-path enforcement, operation limits, project gateway use/missing gateway, and no raw storage driver/catalog exposure. | None |
| SB07 | 07-07-policy-observability-and-sanitization | Completed | Added workflow executor audit records/observer, redacted settings and exception summaries, audit-scope run id propagation, plugin payload cap enforcement, null observer DI registration, and unit proof for settings redaction, observer failure records, plugin id/connection id audit fields, and oversized plugin output rejection. | None |
| SB08 | 08-08-architecture-review-gate-foundations | Completed | Foundation review passed after answering all ten gate questions, confirming no plugin module/project exists, updating the source map for SB03-SB07 behavior changes, and documenting downstream authorization to start SB09. | None |
| SB09 | 09-09-plugins-abstractions-project-and-manifest | Completed | Created `CanDoItAll.Plugins.Abstractions` with strongly typed plugin/package/connection/renderer ids and scalar JSON converters, manifest/source/trust/capability/package/OAuth/settings/connection/workflow-executor contracts, typed capability context interfaces for secrets/workspace files/storage/project/http/OAuth/events, and manifest/catalog validation for duplicate ids, unsupported flags, and missing declared capabilities. Unit proof covers id equality/serialization, duplicate/capability semantics, descriptor round-trip, and no public `IServiceProvider` or implementation-module references. | None |
| SB10 | 10-10-plugins-module-catalog-and-persistence | Completed | Created dedicated Plugins module, bundled catalog source, installation store/entity/migrations, DTO-based catalog/install/enable/disable API, composition/nav/page wiring, and integration proof for catalog, persisted install state, unavailable installed plugins, and OpenAPI routes. Closure also repaired the Workbench project-structure runtime gateway DI cycle and the PostgreSQL workflow-catalog startup warmup query exposed by SB10 validation. | None |
| SB11 | 11-11-plugin-settings-page-and-connection-model | Ready |  |  |
| SB12 | 12-12-workflow-plugin-executor-bridge | Ready |  |  |
| SB13 | 13-13-sample-bundled-plugin | Ready |  |  |
| SB14 | 14-14-architecture-review-gate-plugin-mvp | Ready |  |  |
| SB15 | 15-15-plugin-shop-and-package-contracts | Ready |  |  |
| SB16 | 16-16-oauth2-extension-point-and-connection-broker | Ready |  |  |
| SB17 | 17-17-tests-api-and-browser-proof | Ready |  |  |
| SB18 | 18-18-final-architecture-review-and-closure | Ready |  |  |

## Command Proof Log

| Date/Time | Subbundle | Command | Result | Notes |
| --- | --- | --- | --- | --- |
| 2026-05-13 12:00:09 -04:00 | SB01 | `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\codex\bundles\plugin-workflow-executors\.codex\bundles\plugin-workflow-executors-architecture --stage prepared` | Passed | Initial run failed due validator-required headings; repaired `plan/01-phase-plan.md` and this report, then reran successfully. |
| 2026-05-13 12:00:09 -04:00 | SB01 | PowerShell `Test-Path` over SB01 exact source references | Passed | All 28 exact source references exist in the current checkout. |
| 2026-05-13 12:00:09 -04:00 | SB01 | Source audit of workflow executor contracts/models/validator/descriptors/DI/API/UI, security vault/resolver, workspace file/storage/project-structure seams, connector schema/editor, composition/nav/API registration | Passed | No source-map correction required. Bundle readiness decision remains current: perform foundation hardening before adding the plugin module. |
| 2026-05-13 12:11:35 -04:00 | SB02 | `dotnet build src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj` | Passed | Built core and referenced models after executor metadata/runtime changes. |
| 2026-05-13 12:11:35 -04:00 | SB02 | `dotnet build src\CanDoItAll.AgentFramework.Models\CanDoItAll.AgentFramework.Models.csproj` | Passed | First parallel attempt hit an obj DLL file lock; single-project rerun passed cleanly. |
| 2026-05-13 12:11:35 -04:00 | SB02 | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "WorkflowExecutor"` | Passed | 21 passed, 0 failed, 0 skipped. |
| 2026-05-13 12:31:30 -04:00 | SB03 | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SettingsSchema\|Connector\|WorkflowExecutor"` | Passed | 30 passed, 0 failed, 0 skipped. Initial run exposed canonical schema type fallout and enum numeric select compatibility; repaired before final pass. |
| 2026-05-13 12:31:30 -04:00 | SB03 | `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ConnectorConfig"` | Passed | 2 passed, 0 failed, 0 skipped. |
| 2026-05-13 12:31:30 -04:00 | SB03 | `dotnet build src\CanDoItAll.Modules.Workspace\CanDoItAll.Modules.Workspace.csproj` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 12:58:50 -04:00 | SB04 | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SettingsRenderer"` | Passed | 3 passed, 0 failed, 0 skipped. |
| 2026-05-13 12:58:50 -04:00 | SB04 | `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "SettingsRenderer\|ConfigurationField"` | Passed | 2 passed, 0 failed, 0 skipped. |
| 2026-05-13 12:58:50 -04:00 | SB04 | `dotnet build src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 13:19:00 -04:00 | SB05 | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SecretRuntime\|SecretBroker\|Vault"` | Passed | 14 passed, 0 failed, 0 skipped. |
| 2026-05-13 13:19:00 -04:00 | SB05 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "Secret"` | Passed | 1 passed, 0 failed, 0 skipped. |
| 2026-05-13 13:19:00 -04:00 | SB05 | `dotnet build src\CanDoItAll.Modules.Security\CanDoItAll.Modules.Security.csproj` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 13:41:51 -04:00 | SB06 | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "WorkspaceFile\|ProjectStructure\|PluginCapability"` | Passed | 34 passed, 0 failed, 0 skipped. Initial run exposed that the new facade test expected an exception while the underlying file service returns a denied result; tightened plugin facade to reject rooted filesystem paths directly, then reran successfully. |
| 2026-05-13 13:41:51 -04:00 | SB06 | `dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 13:41:51 -04:00 | SB06 | `dotnet build src\CanDoItAll.Modules.Workbench\CanDoItAll.Modules.Workbench.csproj` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 13:51:10 -04:00 | SB07 | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "Redaction\|WorkflowEvent\|PluginPolicy"` | Passed | 3 passed, 0 failed, 0 skipped. |
| 2026-05-13 13:51:10 -04:00 | SB07 | `dotnet build src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 13:54:49 -04:00 | SB08 | Foundation review source inspections and `git diff --name-only`/plugin project search | Passed | Confirmed no `CanDoItAll.Modules.Plugins` or plugin abstraction project exists before gate passage; reviewed completed SB01-SB07 source boundaries and updated stale source-map entries. |
| 2026-05-13 13:56:15 -04:00 | SB08 | `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\codex\bundles\plugin-workflow-executors\.codex\bundles\plugin-workflow-executors-architecture --stage prepared` | Passed | Bundle remained valid after SB08 review records and source-map updates. |
| 2026-05-13 14:10:22 -04:00 | SB09 | `dotnet build src\CanDoItAll.Plugins.Abstractions\CanDoItAll.Plugins.Abstractions.csproj` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 14:10:22 -04:00 | SB09 | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "PluginManifest\|PluginAbstractions"` | Passed | 6 passed, 0 failed, 0 skipped. Initial run exposed a test helper descriptor-argument mismatch; corrected the test to match the abstraction contract, then reran successfully. |
| 2026-05-13 14:44:54 -04:00 | SB10 | `dotnet build src\CanDoItAll.Modules.Workbench\CanDoItAll.Modules.Workbench.csproj` | Passed | 0 warnings, 0 errors. Rerun after replacing the runtime gateway dependency on `ProjectStructureAgentService` with lower-level project/workbench services to remove the workflow-runtime DI cycle. |
| 2026-05-13 14:44:54 -04:00 | SB10 | `dotnet build src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj` | Passed | 0 warnings, 0 errors. Rerun after simplifying the workflow catalog latest-version query that failed during PostgreSQL startup warmup. |
| 2026-05-13 14:44:54 -04:00 | SB10 | `dotnet build src\CanDoItAll.Modules.Plugins\CanDoItAll.Modules.Plugins.csproj` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 14:44:54 -04:00 | SB10 | `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "PluginCatalog\|PluginInstallation"` | Passed | 3 passed, 0 failed, 0 skipped. Covers bundled catalog, persisted installation state, unavailable installed plugin projection, and plugin API/OpenAPI routes. |
| 2026-05-13 14:44:54 -04:00 | SB10 | `dotnet build CanDoItAll.slnx` | Passed | 0 warnings, 0 errors. |
| 2026-05-13 14:44:54 -04:00 | SB10 | Playwright CLI open/snapshot/screenshot for `http://localhost:5032/plugins`; console warning check; server log scan for `fail:`, `crit:`, `Exception`, `error:` | Passed | Catalog shell rendered after database confirmation. Browser console had 0 warnings/errors; server log scan found no startup failures after the workflow catalog query repair. |
| 2026-05-13 14:44:54 -04:00 | SB10 | `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\codex\bundles\plugin-workflow-executors\.codex\bundles\plugin-workflow-executors-architecture --stage prepared` | Passed | Bundle remained valid after SB10 records and source-map updates. |

## Browser Proof Log

| Date/Time | Subbundle | Route | Viewport | Screenshot/Artifact | Review Notes |
| --- | --- | --- | --- | --- | --- |
| 2026-05-13 12:58:50 -04:00 | SB04 | `http://localhost:5032/agents/workflows` | `1440x1000` | `artifacts\sb04-settings-renderer\sb04-workflow-settings-desktop.png` | Workflow editor opened, Workspace files executor node added from toolbox, fallback fields rendered: Operation, Path, DestinationPath, Content, ContentFromInput, Query, SearchPattern, MaxResults, MaxCharacters, MaxLines, Overwrite. Existing `EmptyProjectionMember` workflow catalog warning appeared but did not block editor fallback proof. |
| 2026-05-13 12:58:50 -04:00 | SB04 | `http://localhost:5032/agents/workflows` | `390x900` | `artifacts\sb04-settings-renderer\sb04-workflow-settings-narrow.png` | Narrow layout screenshot shows the same schema fallback settings fields stacked in the selected executor node editor. |
| 2026-05-13 14:44:54 -04:00 | SB10 | `http://localhost:5032/plugins` | full page | `artifacts\sb10-plugins-catalog\sb10-plugins-catalog-route.png` | Plugins navigation entry and catalog shell rendered with Catalog/Installed/Enabled/Unavailable counters and empty bundled-source state. Browser console had 0 warnings/errors; server startup log was clean after the workflow catalog query repair. |

## Architecture Review Decisions

| Gate | Status | Decision | Required Repairs | Reviewer Notes |
| --- | --- | --- | --- | --- |
| SB08 Foundation Review | Passed | SB09 may start. Foundation hardening is sufficient for bundled plugin abstractions/module work. | None | The only review repair was stale source-map text; corrected before passing the gate. |
| SB14 Plugin MVP Review | Pending | | | |
| SB18 Final Review | Pending | | | |

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| SB01 | Passed | Passed | SB02-SB08 foundation dependencies checked at source-reference level | Proceed to SB02 | Source audit only; no product code changed. |
| SB02 | Passed | Passed | SB03, SB04, SB07, SB12, SB17 dependencies checked | Proceed to SB03 | Executor metadata now represents built-in/planned/plugin-owned/unavailable shapes without changing built-in executor ids. |
| SB03 | Passed | Passed | SB04, SB05, SB08, SB11, SB12, SB17 dependencies checked | Proceed to SB04 | Configuration schema/state/validation are canonical in SharedKernel; Workspace and Resources adapters consume canonical descriptors without duplicating plugin-only schema types. |
| SB04 | Passed | Passed | SB08, SB11, SB12, SB17 dependencies checked | Proceed to SB05 | Settings renderer registry and fallback host now remove the need for hard-coded per-plugin settings UI branches; downstream settings page and plugin executor bridge can consume the same canonical schema surface. |
| SB05 | Passed | Passed | SB07, SB08, SB11, SB12, SB13, SB16, SB17 dependencies checked | Proceed to SB06 | Plugin connection secrets require persisted consumer/purpose bindings through `SecretReference`; runtime provider/storage paths no longer use the editing service to retrieve secret values. |
| SB06 | Passed | Passed | SB07, SB08, SB09, SB12, SB13, SB17 dependencies checked | Proceed to SB07 | Plugin-safe file/storage/project-structure capability seams exist before plugin module work. Project-structure access is available through a stable runtime gateway without concrete Workbench leakage, and normal plugin storage capability does not expose storage drivers or catalog records. |
| SB07 | Passed | Passed | SB08, SB10, SB12, SB13, SB15, SB17 dependencies checked | Proceed to SB08 foundation architecture review | Plugin executor execution can be observed through redacted audit records with workflow/run/node/executor/plugin/connection identity, plugin payload output is capped, and invocation failures no longer expose raw secret-looking values. |
| SB08 | Passed | Passed | SB09-SB13 MVP dependencies checked | Proceed to SB09 | Foundation review passed. No plugin module exists yet; canonical settings, executor metadata/availability, consumer-bound secrets, plugin-safe facades, redacted observability, and proof records are in place. |
| SB09 | Passed | Passed | SB10, SB11, SB12, SB13, SB15, SB16, SB17 dependencies checked | Proceed to SB10 | Plugin contracts are in a separate abstractions project with only SharedKernel and AgentFramework.Models project references; public contracts avoid `IServiceProvider` and implementation modules while preserving future shop package and OAuth metadata seams. |
| SB10 | Passed | Passed | SB11, SB12, SB13, SB14, SB15, SB17 dependencies checked | Proceed to SB11 | Plugins module catalog and install state are separate from connection settings and deterministic. Initial integration validation exposed a Workbench/runtime DI cycle; closure includes the smaller gateway dependency repair before proceeding. |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| SB04 | `/agents/workflows` | `1440x1000`, `390x900` | Browser MCP snapshot verified `Workspace files` executor selected and schema fallback fields rendered. | `artifacts\sb04-settings-renderer\sb04-workflow-settings-desktop.png`; `artifacts\sb04-settings-renderer\sb04-workflow-settings-narrow.png` | Passed |
| SB10 | `/plugins` | full page | Playwright CLI snapshot verified Plugins navigation entry, plugin catalog heading, catalog/install/enabled/unavailable counters, and empty bundled catalog state. | `artifacts\sb10-plugins-catalog\sb10-plugins-catalog-route.png` | Passed |

## Analytics Review

In progress. SB04 has browser proof; SB06 has service-facade proof; SB07 has redacted policy/observability proof, SB08 foundation review passed, SB09 has plugin abstraction contract proof, and SB10 has plugin catalog/install API, persistence, solution build, and browser route proof. Settings/connection UI, plugin executor bridge, sample bundled plugin, API/browser regression matrix, and later architecture reviews remain pending for later subbundles.

## SB08 Foundation Review Answers

| Question | Answer | Evidence |
| --- | --- | --- |
| 1. Is there one canonical settings schema/state/validator? | Yes. | `CanDoItAll.SharedKernel.Configuration` is shared by connector/workflow settings; SB03 proof passed unit/component validation. |
| 2. Did workflow executor descriptors gain enough plugin provenance/availability metadata? | Yes. | `WorkflowExecutorDescriptor` now carries source/trust/availability/settings-schema metadata; planned executors are non-runnable. |
| 3. Are current workflows still backward compatible? | Yes. | Legacy descriptor/settings JSON compatibility and `WorkflowExecutor` regression tests passed during SB02/SB03/SB06/SB07. |
| 4. Can a plugin executor be rejected when disabled/unavailable/incompatible? | Yes. | Validator/invoker reject non-runnable availability states before execution; planned executor tests prove the path. |
| 5. Are secrets consumer-bound by plugin/executor/connection? | Yes. | SB05 strict plugin consumer bindings and broker tests passed; provider/storage runtime paths use `ISecretRuntimeResolver`. |
| 6. Are storage/workspace/project-structure services exposed only through facades? | Yes. | SB06 added `IPluginWorkspaceFiles`, `IPluginStorageGateway`, and `IProjectStructureRuntimeGateway`; raw storage drivers and Workbench service lookup are not default plugin-facing seams. |
| 7. Did helper code end up in canonical services rather than pages? | Yes. | Schema validation/redaction/facade logic lives in SharedKernel/Core/Security/Workspace services; UI consumes renderer host/fallback components. |
| 8. Are duplicate registration paths resolved or explicitly documented? | Yes. | Built-in executor duplicate ids fail fast; settings renderer duplicates fail; project-structure gateway uses unavailable fallback plus Workbench adapter override. |
| 9. Is the plugin module still unimplemented? | Yes. | Source search found no `CanDoItAll.Modules.Plugins`, plugin abstraction project, or plugin module API/routes. |
| 10. Are tests and proof captured? | Yes. | Execution report command log records SB01-SB07 proofs; SB04 screenshots are listed in the browser proof log. |

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| Original request: analyze current codebase readiness and prepare/execute architecture-first plugin workflow-executor plan | Partially solved | SB01 reconfirmed current readiness and prerequisites. Implementation remains in progress across SB02-SB18. |
| R002/R015/R016/R029/R032: plugin executors need provenance, availability, compatibility, and duplicate handling before plugin bridge work | Solved | SB02 code changes and `WorkflowExecutor` unit tests prove descriptor metadata, planned-executor rejection, legacy descriptor JSON compatibility, and duplicate implementation failure. |
| R005/R013/R023/R032: settings schema must be canonical, validated, redacted, and reusable by connectors/workflow executors/plugins | Solved | SB03 introduced `CanDoItAll.SharedKernel.Configuration`, connector compatibility wrappers, workflow executor configuration schemas, canonical validator integration, and `SettingsSchema\|Connector\|WorkflowExecutor` plus `ConnectorConfig` proof. |
| R005/R014/R017/R023/R028/R029/F004/F005: settings UI must use canonical schema fallback plus trusted renderer registry instead of copying hard-coded workflow executor branches | Solved | SB04 added `ISettingsRendererRegistry`, `SettingsRendererHost`, `ConfigurationSchemaFallbackRenderer`, duplicate/invalid renderer tests, workflow canvas integration, and desktop/narrow browser screenshots proving fallback fields render for a Workspace files executor. |
| R004/R011/R012/R019/R034/F006/F007: plugins must resolve secrets through a consumer-bound broker and persist only ids/bindings | Solved | SB05 added strict plugin binding checks in `SecretRuntimeResolver`, `SecretBindingCreateRequest`/`SecretBindingSummary`, `IPluginSecretBroker`, provider/storage runtime resolver usage, and `SecretRuntime\|SecretBroker\|Vault` plus integration `Secret` proof showing unbound plugin connection resolution is rejected without leaking the secret value. |
| R004/R018/R019/R030/F008/F009/F010: plugins need safe workspace, storage, and project-structure capabilities without concrete service leakage | Solved | SB06 added `IPluginWorkspaceFiles`, `IPluginStorageGateway`, `IProjectStructureRuntimeGateway`, a Workbench adapter, direct gateway injection for `ProjectStructureWorkflowExecutor`, and `WorkspaceFile\|ProjectStructure\|PluginCapability` proof for path boundaries, missing gateway behavior, and no raw storage driver/catalog exposure. |
| R020/R021/R024/R035/F002/F006/F009/F014: plugin execution needs observable, redacted, bounded runtime records | Solved | SB07 added `WorkflowExecutorExecutionAuditRecord`, `IWorkflowExecutorExecutionObserver`, `WorkflowExecutorRedaction`, plugin output payload limits, invocation exception sanitization, run-id audit scope propagation, DI registration, and `Redaction\|WorkflowEvent\|PluginPolicy` proof. |
| R026/F001/F002/F003/F004/F005/F006/F010/F015: foundation architecture review must pass before plugin module starts | Solved | SB08 answered all foundation review questions, confirmed no plugin module exists before the gate, corrected stale source-map entries, and authorized SB09. |
| R001/R003/R004/R008/R010/R014/R015/R027/R030/F001/F005/F011/F012: plugin manifests need stable abstractions, capability declarations, settings/renderers, connections, workflow executor contracts, package metadata, and no service-provider escape hatch | Solved | SB09 added `CanDoItAll.Plugins.Abstractions`, manifest/capability/settings/connection/OAuth/package/workflow executor contracts, scalar id JSON converters, validation helpers, and `PluginManifest\|PluginAbstractions` proof for duplicates, missing capabilities, serialization, and public API boundaries. |
| R001/R006/R007/R008/R022/R024/R031/R035/F011/F012/F015: plugin catalog module must be separate, deterministic, persisted, API-backed, and bundled-first | Solved | SB10 added `CanDoItAll.Modules.Plugins`, bundled catalog source, plugin installation EF record/migrations with manifest snapshots, catalog/install/enable/disable API endpoints, static module/nav wiring, plugin catalog page, and `PluginCatalog\|PluginInstallation` integration proof plus `/plugins` browser proof. |
