# C# Boundary Map

## Ownership

| Boundary | Owns | Must not own |
| --- | --- | --- |
| `CanDoItAll.Processes.Contracts` | Stable protocol records/enums shared across runtime, application, templates, and modules. | Module-specific adapter implementation. |
| `CanDoItAll.Processes.Abstractions` | Narrow interfaces required by multiple process layers. | Concrete runtime policies or UI projections. |
| `CanDoItAll.Processes.Runtime` | Recovery classification, retry policy application, subprocess state resolution, runtime tool preflight, artifact ledger behavior. | Workbench-specific launch variable construction or module UI wording. |
| `CanDoItAll.Processes.Application` | Launch orchestration, launch variable enrichment pipeline, process application services. | Adapter finalizer parsing internals or template markdown parsing. |
| `CanDoItAll.Processes.Templates` | Template schema, typed execution class validation, template pack loading. | Runtime recovery decisions. |
| `CanDoItAll.Modules.Processes` | AgentFramework adapter integration and module service registration. | Core process contracts that other projects need. |
| `CanDoItAll.Modules.Workbench` | Project-structure launch variable contribution and workbench-specific process start context. | Generic placeholder resolution semantics. |

## Placement Guidance

- Put `ILaunchVariableTemplateResolver` in the layer that can be reused by launch and rework packet construction. If both application and runtime need it, define the contract in `Processes.Abstractions` or `Processes.Contracts` and keep the implementation in the composition layer.
- Put completion gate result records in `Processes.Contracts` only if runtime, adapter, and projections all consume them. Otherwise keep adapter-local records internal.
- Put recovery classifier policy in `Processes.Runtime`.
- Keep MAF adapter shims in `Modules.Processes`; do not make `Processes.Runtime` reference `Modules.Processes`.
- Put template execution class records and validation rules in `Processes.Templates` with contract records in `Processes.Contracts` if runtime consumes them.

## Boundary Tests

- Existing boundary tests must continue to pass.
- Add tests that prevent runtime from referencing module projects.
- Add tests that prevent template validation from parsing markdown prose for hard gates when typed fields exist.
