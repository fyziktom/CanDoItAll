# SB22 Semantic Invariants

## Provider UI Projection

- The generic memory UI projects provider-specific surfaces from `MemoryProviderManifest.UiSurfaces`; it does not hardcode native Cognitive Memory pages, Qdrant, OpenAI, or RAG provider implementation types.
- A surface can render only when the selected provider is enabled, healthy, and declares the required capability id.
- RCL surfaces render through `DynamicComponent` only after resolving an explicit component key from `IMemoryProviderUiSurfaceComponentRegistry`.
- Iframe and external URL surfaces render only after the configured manifest extension value passes the host URL policy.

## Host UI Policy

- Provider UI URLs must be absolute HTTPS URLs or loopback HTTP URLs.
- Unsafe URL values are never echoed into rendered markup when policy validation fails.
- Missing component registrations, missing capabilities, disabled providers, missing URL settings, and invalid URLs produce explicit fallback diagnostics instead of silently falling back to native UI.
- The generic memory provider editor persists iframe URL configuration under the typed `provider.vendor.uiUrl` extension key only when iframe support is selected.

## Provider Extension Contract

- Future providers can contribute advanced UI by declaring `ui.rcl`, `ui.iframe`, or provider-chosen URL-surface capability requirements in the manifest and registering the matching Blazor component key in the host UI module.
- The generic shell owns projection, policy evaluation, and safe fallback rendering; provider packages own their component implementation and manifest declarations.
- Native Cognitive Memory advanced tabs remain outside the generic Memory module until the later native service extraction subbundles migrate them behind provider-owned packages.

## Boundary

- The generic Memory UI/application/persistence source remains free of native Cognitive Memory, Qdrant, OpenAI, and RAG implementation references.
- Browser proof creates a mock provider through visible UI actions and disables Qdrant only in the Playwright test-host environment.
