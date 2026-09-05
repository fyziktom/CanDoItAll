# AgentsHomePage boundary assessment

Feature-owned route/host component; remains in AgentFramework. Its destination is feature UI host composition, not AppComponents.

Retain current route parameters, compatibility mapping, other tab composition, Workflows button and chat context surface. Remove direct EF and aggregate assembly into cohesive queries. Keep Providers/RequestHistory free of overview/usage/history reads until demanded. Usage-selection loading remains independent of full dashboard loading.

The page/workspace coordination owns semantic state and requested-open decisions. Catalog emits selection/open/chat/team intents; a focused coordinator may dispatch host effects without turning the page into a large imperative service. Editor draft/session does not belong in the workspace state record.

Preserve catalog initial data and SkipCatalogRepair behavior. Move accessible context readiness along with selection; copying only selected IDs loses the meaning used by AgentChatContextSurfaceProvider. Refreshes must reconcile against current semantic identity and cannot publish stale context.

Proof: B01–B08 and B27/B29, exact route/history-host tests, actual overview operation, page/catalog composition, and one public Workflows navigation test. Do not broaden the refactor to provider/voice/governance/Simple Chat implementation internals.

Readiness: semantic and host integration evidence can become proven here; the page retains many unrelated panes and is not the first lightweight sandbox target.
