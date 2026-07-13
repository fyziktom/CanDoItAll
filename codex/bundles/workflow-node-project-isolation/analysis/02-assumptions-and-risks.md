# Assumptions And Risks

## Assumptions

- Workflow value objects and records can remain in `CanDoItAll.AgentFramework.Models` during the first migration stage unless dependency proof shows a dedicated `Workflows.Models` project is required.
- New workflow projects should follow the existing process project pattern rather than inventing a second architecture style.
- MAF-specific Microsoft Agents workflow compiler/backend code can move to a workflow MAF-adapter project or a sharply isolated MAF namespace, but it must not continue owning default executors.
- Existing workflow definitions, template YAML, executor ids, settings renderer keys, and plugin manifests are compatibility contracts.
- The implementation agent may keep transitional adapter methods during migration, but they must be explicit and scheduled for removal in SB14.

## Critical Path Risks

- Contract extraction can create circular references if workflow abstractions keep depending on Core services that should move later.
- Plugin packages can break if `IWorkflowExecutor` moves without a compatibility bridge for installed package discovery and bundled plugin registrations.
- Template validation can reject existing workflow YAML if descriptor availability or executor id resolution changes too early.
- Moving runtime stores without preserving run/checkpoint/artifact JSON shape can break execution history, recovery, and evidence source projection.
- MAF reconnection can become cosmetic if default executors, descriptor factories, or workflow stores still live in MAF after SB11.
- UI and Workbench adoption can mask backend regressions if it proceeds before descriptor parity and runtime proof pass.

## Validation Risks

- Unit-only proof is insufficient because executor catalogs, templates, plugins, side effects, run preview, API endpoints, and Workbench node starts interact.
- Happy-path plugin tests can miss grant-denied, OAuth missing, deterministic preview, idempotent marker, and runtime package source-metadata failures.
- Performance findings may be dismissed as micro-optimizations; checkpoints should target hot-path catalog/template/runtime loops and allocation-heavy JSON/regex helpers only.
- Browser screenshots alone do not prove workflow correctness. UI proof must cite API/runtime state and executor catalog behavior.
- A passing build can still leave architectural regression if new projects reference MAF or Modules where contracts should be module-free.

## Reopen Triggers

- Any subbundle adds a dependency from workflow abstractions or executor abstractions back to MAF, Web, Modules, or plugin implementation projects.
- Any moved executor changes an executor id, settings renderer key, input/result shape, side-effect descriptor, deterministic test-mode descriptor, or source/trust metadata without an explicit compatibility exception.
- Any plugin executor test proves only manifest projection but not runtime invocation.
- Any template loader change silently falls back to old hardcoded definitions when a template file is invalid.
- Any host registration keeps direct per-executor registration in `AgentFrameworkServiceCollectionExtensions` after composition extraction.
- Any MAF adapter phase leaves `BuiltInWorkflowExecutorDescriptors` or default executor implementations in MAF without a planned SB14 cleanup item.
- Any critical proof manifest is missing, prose-only, or lacks failing-first and passing proof for behavior changes.
