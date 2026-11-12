# Provider Profiles UI

This Razor library contains the complete provider catalog, six-tab editor shell and the
Connection, Prices, Runtime and Thinking renderers. It references safe provider Models
and BaseLib; it does not register native provider services or own persistence.

`ProviderProfilesSurface` receives `IProviderProfilesView`. The native module host supplies
the existing whole provider draft, its original `EditContext`, activation identity and
operation intents. `ProviderEditorDraft` owns presentation text, field revisions and raw
validation against that context. Pricing and Thinking reuse the existing model policies.

Typed render slots keep Sharing, request History, source connections and source refresh
with their native module owners. The sandbox identifies those integrations as deferred.
Compatibility types retain their original namespaces and are forwarded by the module.

Use the [independent sandbox](../../Sandboxes/CanDoItAll.AgentFramework.Providers.UiSandbox/README.md)
for 1920×1080 UI work and the [light tests](../../../tests/Components/CanDoItAll.AgentFramework.Providers.UI.Tests/README.md)
for renderer, lifecycle and dependency checks. Native owner and consumer proof remains
separate. See the [PP1 architecture and validation record](../../../docs/architecture/provider-profiles-ui-pp1.md)
for the complete ownership census, proof selections and measured development loop.
