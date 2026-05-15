# Structured Input

## Primary Outcome

Prepare a follow-up bundle that implementation agents can execute without rediscovering the architecture, performance, EF, logging, menu, icon, and Docker ZIP handoff requirements.

## Explicit Non-Goals

- Do not implement product code during bundle preparation.
- Do not remove Gmail or Office365 default registration in this bundle unless a downstream requirement explicitly asks for it later.
- Do not invent a separate plugin UI framework.
- Do not use externally hosted brand icons at runtime.
- Do not hide install/runtime failures with silent fallbacks.

## Implementation Assumptions

- The app should continue to support bundled/default plugins and installed runtime packages.
- Runtime package manifests should be the source of truth for installed package identity, source kind, trust level, and package metadata.
- Concrete plugins should be able to register services and executors, but the generic plugin runtime must not depend on concrete Docker/Gmail/Office365 implementations.
- Plugin logs need durable storage, because `ILogger` output is insufficient for user-facing diagnostics in the plugins page.
- The existing CanvasLib context menu supports nested submenus; the workflow catalog should build the right menu tree instead of changing the menu framework first.

## Required Deliverables

- Six execution-ready subbundles with prerequisites, exact source references, validation gates, and proof requirements.
- A detailed XLSX checklist with references and validation steps.
- A target architecture that separates generic runtime behavior from concrete plugin packages.
- Specific review instructions for performance and EF query risks in plugin runtime paths.
