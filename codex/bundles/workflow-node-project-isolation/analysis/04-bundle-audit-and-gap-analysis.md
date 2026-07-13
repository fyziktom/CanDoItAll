# Bundle Audit And Gap Analysis

## Original Request Coverage

The prepared bundle covers the major architecture request:

- Workflow and node concerns are isolated into workflow-owned abstractions, builders, core, runtime, persistence, template, hosting, and MAF adapter projects.
- Executor contracts/helpers are isolated before default executor and plugin executor implementation moves.
- Default executors are split by logical category, and plugin-provided executors are treated as first-class executor sources.
- The sequence is base-up and dependency-gated: contracts/builders before core/runtime, executor abstractions before executor implementations, plugin boundary before template/MAF/UI adoption, and hardening gates before each dependent block.
- XLSX mapping exists and is part of the bundle proof.
- Forced hardening checkpoints exist at SB05, SB09, and SB13.

The audit found three gaps that needed bundle corrections:

1. Failure/error-state handling was under-specified for external tools/MCPs and plugins.
2. Several planned moves could become copied monoliths if implementation simply moves large classes to new projects.
3. Executor category boundaries were too broad for document, network, and media dependencies.

## CodeAnalytics Evidence

Re-audit snapshot:

- Snapshot id: `snap-20260629143729-e43d210b`
- Scope: workflow, MAF, plugin, Workbench, Web, persistence, and process-adjacent projects.
- Snapshot size: 20 source projects and 587 source documents.
- Blocking diagnostics: none.

Important architecture findings:

- `CanDoItAll.AgentFramework.Maf` has a type cycle.
- `CanDoItAll.Modules.AgentFramework` has module/type cycle findings.
- `IWorkflowExecutor` is currently implemented by default MAF executors, bundled plugin executors, and runtime package wrappers. This confirms that plugins are not a side case.
- `RuntimePackageWorkflowExecutor` currently delegates to the inner executor after descriptor source metadata projection. The extraction must not lose package/plugin/type context when the inner executor throws.

Important large-file/maintainability signals:

- `ProjectStructureWorkflowExecutor.cs` is roughly 700 lines and mixes settings resolution, JSON path handling, project-structure operations, result shaping, and diagnostics.
- `SourceIngestionWorkflowExecutor.cs` is roughly 770 lines and mixes path resolution, file/directory enumeration, content loading, caps, result shaping, and per-file errors.
- `WorkflowTemplatePackLoader.cs` is roughly 700 lines and mixes manifest parsing, workflow materialization, preview simulation loading, DTOs, validation, and diagnostics.
- `MafInProcessWorkflowExecutionBackend.cs` is roughly 680 lines and mixes compile failure handling, backend execution, external request capture, event normalization, payload capture, artifacts, and checkpoint creation.

Bundle correction: R18 and the affected subbundle READMEs now require split-by-responsibility work and checkpoint file-size/responsibility proof.

## Error-State Findings

Current code has useful building blocks:

- The executor invoker records audit events and redacts exception text.
- Timeout and payload-too-large cases have explicit wrappers.
- Validation messages for missing executors, unavailable descriptors, invalid settings JSON, invalid routing, and unsafe retry policies are specific.
- Artifact path protections throw explicit errors.

The concern is that user-facing failure summaries can still depend on exception text parsing and plugin/external executor failures can lose structured source context. That is risky once workflow/executor/plugin code is spread across projects.

Bundle correction:

- Added R17.
- Added `architecture/04-failure-diagnostics-and-error-state-boundary.md`.
- Added `inventories/06-error-state-inventory.md`.
- Strengthened SB02-SB14 to require typed diagnostic envelopes, retryability, repair hints, redaction, and no-generic-error audits.

## Performance And Maintainability Recheck

Broad scoped heuristic scan after the re-audit found:

| Pattern | Broad scoped hits | Planning interpretation |
| --- | ---: | --- |
| `IndexOf(string)` without explicit comparison | 7 | Review during checkpoints; not automatically a defect. |
| `StartsWith`/`EndsWith` without explicit comparison | 171 | Mostly broad-source heuristic; extracted code should preserve explicit comparisons in protocol/path code. |
| `ToLower`/`ToUpper` | 133 | Broad-source heuristic; checkpoint review should prevent culture-sensitive identifier logic. |
| `.Substring(` | 4 | Review only in hot/string parsing helpers. |
| `new JsonSerializerOptions` | 25 | Executor/workflow helper projects should centralize stable options where safe. |
| `new Regex(` | 0 | No uncached construction signal in scoped paths. |
| `RegexOptions.Compiled` | 6 | Prefer generated regex for static patterns during extraction where appropriate. |
| `[GeneratedRegex]` | 12 | Positive existing pattern. |
| `new HttpClient` | 7 | Broad-source heuristic; executor moves must not create new client ownership. |
| LINQ chain candidates | 3671 | Too broad for direct action; checkpoint scans should focus on descriptor, template, plugin projection, and execution loops. |
| `async void` | 0 | No broad signal. |

Planning decision: do not add speculative optimization work. Require focused scans at SB05, SB09, and SB13 and targeted fixes only where repeated runtime/template/descriptor/plugin paths are affected.

## Final Audit Decision

The bundle is logically sequenced and appropriate after the corrections. Implementation should proceed only through the subbundle order and must not skip the diagnostic and refactoring-hardening gates. The highest-risk phases are SB06-SB09 because executor abstractions and plugin adapters define the failure, security, side-effect, and compatibility behavior for every later phase.
