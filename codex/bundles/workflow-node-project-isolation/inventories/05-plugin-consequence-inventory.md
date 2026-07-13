# Plugin Consequence Inventory

| Plugin surface | Current reference | Consequence | Owning subbundle |
| --- | --- | --- | --- |
| Plugin executor manifest descriptor | `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs:68` | Must remain compatible or provide adapter. | SB08 |
| Plugin runtime execution contract | `repo://src/CanDoItAll.Plugins.Abstractions/PluginExecutionContracts.cs:23` | May need bridge to new executor abstractions. | SB08 |
| Bundled plugin registration | `repo://src/plugins/*/*PluginServiceCollectionExtensions.cs` | Registrations currently add `IWorkflowExecutor`; migration must support old/new during transition. | SB08 |
| Installed package scanning | `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs:1041` | Runtime package types discovered by interface assignability; abstraction move can break packages. | SB08 |
| Descriptor source | `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginWorkflowExecutorDescriptorSource.cs:7` | Must preserve grant/source/trust/availability mapping. | SB08 |
| Plugin grants | `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginGrantEvaluator.cs` | Grant-denied executors must remain visible as unavailable, not disappear silently. | SB08, SB09 |
| OAuth and secrets | Gmail/Office365 plugin executors | Missing connection/token must fail predictably with actionable diagnostics. | SB08, SB14 |
| Host command | Docker plugin executors | Host command grant and approval requirement must remain enforced. | SB08, SB14 |
| Side-effect receipts | Gmail/Office365 mark-processed executors | Production mutation proof must cite receipts, idempotency keys, and lifecycle. | SB08, SB14 |
| Deterministic preview | Gmail/Office365/Docker simulation templates | Preview must not call external services or mutate external systems. | SB08, SB09 |
| Plugin UI display | `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginExecutorsTab.razor` | Display must keep source, policy, grant, settings, and trust summaries. | SB12 |
| Plugin failure diagnostics | Runtime package adapter, bundled plugin executors, audit sink | Failures must include plugin id, package id, executor id, executor type, source/trust, operation/tool/provider context, retryability, redacted technical detail, and repair hint. | SB08, SB09, SB12 |
| Plugin dependency and activation errors | Runtime package load context and DI activation | Missing assembly/dependency/service must not surface as generic executor failure; diagnostics must point to the package/type/dependency when safe. | SB08, SB09 |

## Plugin Migration Strategy

1. Add new executor abstraction bridge while accepting current `IWorkflowExecutor` registrations.
2. Move descriptor projection logic into plugin executor adapter project.
3. Migrate bundled plugins to new abstractions after compatibility proof.
4. Keep installed package scanning backward-compatible for one migration window.
5. Add cleanup guard that blocks new runtime packages from depending on obsolete MAF/Core executor contracts after SB14.

## Required Negative Plugin Proof

- Runtime package assembly cannot load because a dependency is missing.
- Runtime package executor type cannot be activated by DI.
- Plugin executor throws during execution.
- Plugin grant is missing or denied.
- OAuth/secret is missing, expired, or unavailable.
- Docker host command fails with non-zero exit.
- Gmail/Office365 provider returns rate limit or service unavailable.
- Plugin diagnostic redaction masks tokens, secrets, authorization headers, email bodies, file contents, and host-command sensitive arguments.
