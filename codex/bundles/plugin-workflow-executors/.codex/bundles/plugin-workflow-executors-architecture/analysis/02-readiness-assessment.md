# Readiness Assessment

## Verdict

CanDoItAll is ready for a plugin-system preparation track, not for a full plugin runtime/shop implementation.

## Why Preparation Can Start

- Workflow executors are already a formal concept.
- Workflow executor descriptors and the executor catalog make plugin executors a natural extension.
- Improved secret vault storage gives a vault abstraction that can be turned into a plugin secret broker.
- Workspace file services already centralize path-policy-based file access.
- Connector manifests and config field editors show an existing dynamic settings pattern.
- Codex bundle conventions and review gates already exist.

## Why Plugin Module Must Wait

The plugin module would amplify current weak seams:

- hard-coded settings UI would become unmaintainable with every plugin;
- secrets would be easy to reference without consumer ownership enforcement;
- project structure access would spread concrete Workbench dependencies;
- storage driver exposure could bypass path/policy boundaries;
- remote shop planning could accidentally imply arbitrary code loading without trust design;
- duplicate executor registration paths could make plugin catalog behavior inconsistent.

## Minimal Safe Start

Start `SB01` through `SB08` first. These subbundles create the canonical seams that prevent duplication and ensure plugins can be isolated.

## MVP Start Condition

`CanDoItAll.Modules.Plugins` may start only after `SB08` confirms:

- executor descriptors and validators are plugin-ready;
- settings schema/renderer host is canonical;
- secret runtime authorization is consumer-bound;
- plugin-safe service facades exist;
- execution observability/sanitization policy exists;
- there is no unresolved duplicate registration problem.
