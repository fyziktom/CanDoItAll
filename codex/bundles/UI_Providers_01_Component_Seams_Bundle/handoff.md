# Next bounded work: Providers-02

Prepare and validate the commands/effects child before executing it. No command outcome taxonomy is prescribed by this state/read child.

Trace IProviderRuntimeAdministrationService -> IProviderProfileRegistry and identify validation, persistence, cache/secondary effects and cancellation boundaries. Existing save/delete/health/pricing calls remain in AgentProviderProfilesPanel. Source-managed authorization must still be enforced by the backend as well as UI.

Required next behavior matrix: rejected vs committed vs warning vs unknown where the actual backend supports those distinctions; returned first-save identity before dependent catalog/editor reconciliation; preservation of edits made during saves/refresh; failed post-commit reload without a duplicate create; cancellation timing; stale callbacks; selected deletion; shared-source overlay ownership and pending effects. Current reads fail closed and Retry retains the read target, but mutation reconciliation is not yet hardened.

Keep ProviderRequestHistoryPanel internals outside that child. Keep six section IDs and lazy form boundaries stable. After both provider-profile children, prepare the AgentCatalogPanel light UI assembly/catalog sandbox checkpoint and measure actual warm dotnet-watch latency before choosing another large hotspot.
