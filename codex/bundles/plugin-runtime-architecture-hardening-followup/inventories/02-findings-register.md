# Findings Register

| Id | Severity | Finding | Source References | Owner Subbundle |
|---|---:|---|---|---|
| FIND-001 | Critical | Runtime package assembly auto-registration can import bundled plugin descriptors and conflict with package manifest identity. | `PluginPackageServices.cs:514`, `PluginPackageServices.cs:800`, `PluginPackageServices.cs:801`, `DockerBundledPlugin.cs:20` | SB01 |
| FIND-002 | High | Installed package discovery recursively scans nested manifests. | `PluginPackageServices.cs:299`, `PluginPackageServices.cs:787` | SB01, SB05 |
| FIND-003 | High | Plugin runtime and installation logs are not durable/user-visible. | `WorkflowExecutorObservability.cs:34`, `AgentFrameworkModuleServiceCollectionExtensions.cs:88`, `PluginExecutionContracts.cs:275` | SB02 |
| FIND-004 | Medium | Plugins page/catalog still uses bundled-only wording and fallback identity. | `PluginsPage.razor:24`, `PluginsPage.razor:131`, `PluginCatalogServices.cs:186`, `PluginCatalogServices.cs:270` | SB01, SB02 |
| FIND-005 | Medium | Concrete plugin namespaces and project dependencies still look module-owned. | `src\plugins\...\*.cs`, plugin `.csproj` references to `CanDoItAll.Modules.Plugins` | SB01 |
| FIND-006 | High | Workflow canvas right-click menu lists plugin executors directly under `Executors`. | `WorkflowExecutorCanvasCatalog.cs:14`, `WorkflowExecutorCanvasCatalog.cs:39` | SB03 |
| FIND-007 | Medium | Icon contract is string/path based and not shared cleanly across plugin page, menu, and executor node. | `WorkflowExecutorModels.cs:358`, `PluginPackageModels.cs:101` | SB04 |
| FIND-008 | Medium | Latest connection lookup materializes before ordering. | `PluginPermissionServices.cs:146`, `PluginPermissionServices.cs:155`, `PluginPermissionServices.cs:157` | SB05 |
| FIND-009 | Medium | OAuth workflow connection resolution materializes joined candidates before latest selection. | `PluginOAuthService.cs:329`, `PluginOAuthService.cs:364`, `PluginOAuthService.cs:365` | SB05 |
| FIND-010 | Medium | Executor descriptor availability can trigger repeated sync DB reads while building catalogs/UI. | `PluginPermissionServices.cs:32`, `PluginPermissionServices.cs:278`, concrete executor `Descriptor` properties | SB05 |
| FIND-011 | Critical | Docker cannot be a valid manual runtime ZIP handoff while it remains default-registered. | `RuntimeHostServiceCollectionExtensions.cs:54`, `DockerPluginServiceCollectionExtensions.cs:10` | SB06 |
| FIND-012 | High | Existing package tests do not prove real assembly/executor activation. | `PluginCatalogIntegrationTests.cs:121`, `PluginCatalogIntegrationTests.cs:703` | SB01, SB06 |
