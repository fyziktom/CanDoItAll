# Bookmarkability context

The supplied ZIP proposes hybrid path/query navigation, routed dialogs, a route/state codec, compatibility handling, and phased migration. It does not settle a canonical path-only policy. URL shape, history push/replace behavior, dirty-draft navigation, Workbench identities/windows, and MAUI host behavior remain product/architecture decisions.

This child prepares typed section/target/intents and compatibility mappings while retaining current /agents routes, query keys, and history semantics. Search text, expansions, working drafts, busy state, and confirmation overlays do not become route state merely because they exist.

BaseLib DialogService currently calls CloseAll on LocationChanged and copies parameters into DialogReference. Typed section callbacks cannot alone retain an editor across navigation. A later routing implementation needs an explicit host/session lifetime solution, such as declarative dialog ownership or a justified route-aware host adapter. Do not change global dialog behavior or require a sibling-library edit in this child.

Record six readiness dimensions separately; semantic navigation preparation is not working bookmarkability. Physical UI extraction and a small dotnet-watch host do not depend on deciding or shipping production URLs.
