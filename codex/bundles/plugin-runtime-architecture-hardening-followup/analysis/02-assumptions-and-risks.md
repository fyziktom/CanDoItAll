# Assumptions And Risks

## Assumptions

- Bundled plugins remain supported for Gmail and Office365 during this bundle.
- Docker is the only plugin that must be removed from default app registration and handed off as a runtime package ZIP.
- Runtime package install/upload already has baseline behavior from the previous bundle.
- The implementation agent may add small internal models and services when they enforce typed contracts or testability.
- The implementation agent should prefer extending existing plugin/runtime stores over creating a parallel subsystem.

## Critical Path Risks

- Runtime package activation can double-register plugin identity if package assemblies continue to register `ICanDoItAllPlugin` bundled descriptors.
- Docker ZIP validation will be misleading unless it proves actual assembly loading and executor discovery, not just manifest installation.
- Recursive installed manifest scanning can discover nested manifests and produce false packages.
- Plugin runtime logs can leak settings, OAuth details, command arguments, or user data if redaction is not applied consistently.
- Moving executor availability evaluation from per-descriptor sync DB reads to a batch/cached model can affect UI timing and tests.
- Brand icons may be subject to trademark or usage restrictions. The implementation should use reviewed local assets or fall back to neutral Material icons.

## Validation Risks

- Component tests can pass while the workflow canvas nested menu fails in the browser because submenu positioning and hover/click behavior are JavaScript-driven.
- Package tests can pass with manifest-only fixtures while real package assemblies fail after restart.
- Build tests can pass with Docker still referenced transitively; the closure gate must prove the app starts without default Docker registration.
- Performance fixes can regress ordering semantics if in-memory filtering is moved into EF queries without preserving latest-connection behavior.

## Reopen Triggers

- Docker remains registered from `RuntimeHostServiceCollectionExtensions` or another default host path after subbundle 06.
- A runtime package assembly can contribute a bundled `PluginDescriptor`.
- Installed package discovery still uses recursive manifest enumeration under the installed root.
- Plugins page cannot show durable installation and runtime logs separately.
- Workflow canvas right-click menu still lists plugin executors directly in the second `Executors` layer.
- Executor nodes cannot render plugin icons independently from generic executor icons.
- Tests do not include at least one package assembly fixture with workflow executor discovery.
