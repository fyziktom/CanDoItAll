# Normalized Requirements

| Id | Requirement | Acceptance target | Owning subbundles |
| --- | --- | --- | --- |
| R01 | Preserve the raw request and prepare only; do not implement production code during preparation. | Bundle files and XLSX are the only created artifacts. | SB01-SB14 |
| R02 | Inventory all workflow, workflow node, executor, plugin, template, API, UI, persistence, and test surfaces before defining project moves. | `inventories/*.md` and workbook map every surfaced area to an owner. | SB01 |
| R03 | Define a base-up project graph for workflow-owned abstractions, builders/factories, core services, runtime services, stores, templates, executor abstractions, executor implementations, plugin adapters, and MAF adapter. | `architecture/01-target-solution.md` and `architecture/02-project-map-and-adoption-boundary.md` list projects and dependency rules. | SB01, SB02 |
| R04 | Isolate workflow builders and factories so components/tests do not hand-roll definitions, nodes, edges, ports, and executor nodes repeatedly. | Builder/factory project owns graph construction, template-to-model conversion helpers, and test fixtures. | SB02, SB03, SB10 |
| R05 | Move workflow core services out of catch-all Core into workflow-owned projects without breaking model compatibility. | Validator, catalog contracts/services, routing compiler, preview simulation, failure formatter, payload policy, and process bridge have owned project targets and tests. | SB03, SB05 |
| R06 | Move runtime, stores, event sinks, checkpoint/artifact/external request contracts, and backend catalog into workflow-owned runtime/store projects. | Runtime services are not registered ad hoc from `AddAgentFrameworkCore`; stores and artifact content paths have isolated tests. | SB04, SB05 |
| R07 | Create executor abstractions and shared helpers in executor-owned projects. | `IWorkflowExecutor`, catalog, invoker, descriptor source, observability, redaction, policy limits, JSON/settings helpers, and approval gate contracts have dedicated projects and tests. | SB06, SB09 |
| R08 | Split default executor implementations by logical category, not one MAF-owned bucket. | Control, data transform, workspace/file, source ingestion, network, document/spreadsheet, image/media, and project-structure executors have clear implementation owners. | SB07, SB09 |
| R09 | Keep plugin-provided executors first-class and compatible. | Plugin manifest, grant, package loading, runtime registration, bundled plugin executors, and descriptor projection are migrated with parity tests. | SB08, SB09 |
| R10 | Move workflow template loading and descriptor materialization to a workflow template project. | `Templates/Workflows` loader no longer lives in a Blazor module; template validation references executor descriptors through abstractions. | SB10, SB13 |
| R11 | Isolate MAF workflow compiler/backend/adapters without leaving MAF as the workflow hub. | MAF only composes adapter services after workflow and executor projects pass gates. | SB11, SB13 |
| R12 | Adopt isolated projects in API, Blazor workflow UI, project-structure workflow nodes, and tests only after backend gates pass. | UI/API/Workbench services depend on workflow contracts/services, not MAF internals. | SB12, SB13 |
| R13 | Add forced refactoring-hardening checkpoints after foundation, executor/plugin, and adoption blocks. | SB05, SB09, and SB13 block downstream phases until architecture, tests, diagnostics, and performance scans pass. | SB05, SB09, SB13 |
| R14 | Preserve executor ids, template keys, workflow JSON compatibility, event records, side-effect receipts, and deterministic test-mode semantics. | Regression tests prove old definitions/templates/plugins still load and execute or fail with explicit diagnostics. | SB07-SB14 |
| R15 | Capture artifact-backed proof requirements for critical subbundles. | Critical READMEs require `proof/SBxx/manifest.md`, semantic invariants, source assertions, negative proof, passing proof, hashes, and anti-stub audits. | SB02-SB14 |
| R16 | Produce XLSX mapping of rework surfaces, project targets, subbundles, risks, and validation. | `inventories/workflow-node-project-isolation-map.xlsx` exists, renders, and is linked from the bundle. | SB01 |
| R17 | Preserve and improve actionable workflow failure diagnostics across validation, runtime, executor, external tool/MCP, plugin, persistence, artifact, approval, timeout, and cancellation paths. | Failures expose a typed, user-safe diagnostic envelope with node id, executor id, source/plugin/package/tool context, retryability, repair hint, masked technical detail, and secure-log correlation; no implementation may collapse failures to generic messages such as "executor start failed." | SB02-SB14 |
| R18 | Prevent moved workflow/executor/template code from becoming new monoliths in new projects. | Large files such as source ingestion, project-structure executor, template loader, runtime backend, and validators are split by responsibility with helper/service tests; checkpoints block copied whole-file moves that keep old coupling. | SB05, SB07, SB09, SB10, SB11, SB13 |

## Literal Language Preservation

- `all parts` means all discovered workflow/executor/plugin/template/API/UI/test surfaces listed in inventories must be assigned to a subbundle or explicit exception.
- `executors must have own` means executor contracts/helpers and implementations cannot remain hidden in MAF; they need executor-owned projects and category owners.
- `plugins` means bundled and runtime package executors, grant checks, trust/source metadata, OAuth/secrets, host commands, side effects, deterministic preview, package loading, and UI display.
- `base up` means no runtime adoption before contracts, core, runtime/store, executor, plugin, and template foundations pass gates.
- `checkpoints forced refactoring-hardening` means SB05, SB09, and SB13 are blocking subbundles, not optional cleanup.
- `properly process exceptions and other error states` means failures must retain enough structured context for a user or agent to repair the workflow without scraping exception text or opening unrelated logs.
- `better maintainability` means moving code is insufficient when the moved file remains oversized or mixes parsing, IO, policy, runtime, and UI concerns; implementation must split responsibilities at the same time as ownership changes.
