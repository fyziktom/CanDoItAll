# SB04 explicit blank-client initialization

## Current state and boundary ownership

Fresh startup always persists five provider defaults in AppDatabaseBootstrapper.
DatabaseProviderRuntimeProfileSnapshotLoader adds synthetic Remote Ollama, and
DatabaseProviderProfileRegistry redundantly inserts it again. Deleting rows is not
a durable blank setup: startup recreates them. This invalidates setup-only planning.

Target: one typed ProviderInitializationOptions.SeedDefaults (default true), owned
by ProviderManagement, bound by Composition. Bootstrap checks it before provider
or secret seeding. The canonical runtime loader owns optional fallback inclusion;
registry consumes its result unchanged. Existing explicit providers remain usable.

Live fresh UI exposed a second stale projection: dashboard/overview totals counted
file-seeded providers, although the actual canonical list was empty. The existing
workspace owner now obtains its totals from catalogService.ListProvidersAsync; no
new dependency or owner. The integration contract asserts all five totals surfaces
at zero, at one after explicit configuration, and under compatible default startup.

## Dependency direction and pattern decision

Composition -> AgentFramework module -> ProviderManagement remains unchanged.
No new projects/references or interface; no partial file. Plain typed options suffice.
Reject deleting rows after each start, fixture-only skips and copying existing data.
Keep existing seed recipes untouched; registry shrinks by removing duplicate policy.
This is bounded initialization configuration, not a general bootstrap refactor.

CodeAnalytics before: snap-20260827185022-b43fde6e, one project/70 documents,
245 edges, zero scoped cycles; eight informational DI-factory interpretation warnings.
Project file confirms existing outward references (snapshot single-project reference
array omits external projects). No whole-solution or absence-of-debt claim.

## Testability contract and checkpoints

Add two ProviderInitializationIntegrationTests: unconfigured default behavior remains
compatible; disabled defaults yields no persisted/runtime providers and rejects fallback
ID, survives repeated initialization, allows manual provider save and preserves it.
Use real PostgreSQL harness; no external model call. Expected two cases, failing-first.
Run ProviderCatalogProjectionFailureTests (12 existing methods, discovery controls actual
data rows) for registry delegation and existing mutation behavior. Run 15 avatar cases.
No full-suite trigger: no schema/public runtime protocol/project graph changes.

Entry checkpoint Pass: source owners and test seam read; before graph inspected.
Closure requires actual blank Docker UI after restart plus manual-save integration proof,
existing pair health and unchanged source provider profiles. Existing API auth unchanged.

Closure: Pass. Final proof is bundle://proof/SB04/manifest.md. Additional workspace
evidence regressions pass (five existing + two initialization cases). The final image
was deployed to all three apps; fresh zero counts survive recreation. Scoped after
snapshot snap-20260827191152-b43fde6e has 71 documents, 245 edges, zero cycles and the
same eight informational diagnostics. No project/reference changes.
