# Forbidden patterns

    Codex must not close this item while any of these remain active:

    - `Enum.GetValues<ProviderKind>()`
- `Enum.GetValues<ResourceKind>()`
- `@switch (editor.ResourceKind)`
- `TryResolve(ProviderKind providerKind, string? connectorPluginKey, out IProviderAdapter adapter)`
- `ResolvePluginKey(ProviderKind providerKind)`
