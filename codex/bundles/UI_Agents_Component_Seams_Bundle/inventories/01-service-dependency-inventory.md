# Service dependency inventory

Injection counts are observed diagnostics, never quotas.

| Owner | Observed coupling | Planned responsibility boundary |
|---|---|---|
| AgentsHomePage | Workspace, ProviderUsageQueryService, chat launcher, catalog warmup, EF factory, navigation, notification/dialog | Route and host stay UI-owned; overview/catalog/usage operations retain lazy regions and preserve context projection |
| AgentCatalogPanel | Workspace, provider runtime administration, catalog repair, notification/dialog, chat launcher | Controlled rendering plus typed intents; cohesive catalog operations and focused host dispatch |
| AgentDetailsDialog | Workspace, provider administration, ProjectsService, SecretService, external target registry, notification/dialog | Editor session/presentation plus cohesive operations; real adapters and pure normalization; independent reference failures |
| Descendants | Storage source/dialog, external registry, shared provider management, avatar gateway, capability setup service, memory store/drivers | Inventory and exercise real child boundaries; ownership remains explicit |

Candidate operation names: AgentsOverviewQuery, AgentCatalogController, AgentEditorController. They express responsibilities rather than an approved fixed count. Split a cohesive reference-data operation or introduce a narrow Projects/Secrets adapter capability if required for meaningful tests. Do not move every underlying service method into a facade.

Before finalizing a new operation, record: constructor dependencies, statelessness/lifetime, production caller/registration, fakeable external capabilities, pure rules, explicit error/commit outcomes, direct tests, integration tests, and public result type owners.

Navigation and dialog/notification/chat presentation belong to the host/UI layer. Application operations must not require NavigationManager, DialogReference, components, or callbacks that obscure committed work. Logging includes actionable masked state; error outcomes remain explicit.

Avoid IServiceProvider, service bags, generic component bases, circuit-wide mutable sessions, and a controller whose test still requires uninitialized concrete services.
