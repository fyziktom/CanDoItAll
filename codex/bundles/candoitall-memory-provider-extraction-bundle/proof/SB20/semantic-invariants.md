# SB20 Semantic Invariants

## UI Boundary

- `SB20_UI001`: The `/memory` route is owned by the generic Memory UI module and does not resolve to the native Cognitive Memory page.
- `SB20_UI002`: The generic Memory UI module must not reference native Cognitive Memory implementation types, Qdrant, or AgentFramework RAG driver types.
- `SB20_UI003`: Provider profiles are rendered from generic memory provider profiles and manifests only.

## Provider Lifecycle

- `SB20_PROVIDER001`: Zero-provider startup renders provider management and keeps provider-backed actions disabled.
- `SB20_PROVIDER002`: Demo providers are created only through an explicit UI action; no mock, native, Qdrant, or OpenAI provider is auto-created during route load.
- `SB20_PROVIDER003`: Provider health and capability display is provider-agnostic and uses manifest capability ids instead of native feature assumptions.

## Navigation And Runtime Composition

- `SB20_NAV001`: Shell navigation contributes a generic `Memory` route before the legacy native `Cognitive Memory` route.
- `SB20_NAV002`: Runtime composition registers the generic memory runtime and generic UI module without requiring native Cognitive Memory services to instantiate the `/memory` page.

## Browser Proof

- `SB20_BROWSER001`: Browser proof must cover zero-provider startup, explicit two-demo-provider creation, provider detail/capability rendering, disabled query behavior, a provider health/error state, and both desktop and narrow mobile viewports.
