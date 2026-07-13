# Template-bound .NET bootstrap decision (2026-07-11)

> Superseded for solution topology by [08-dotnet-solution-context-and-initialization.md](08-dotnet-solution-context-and-initialization.md). The binding and path-safety principles remain valid; a solution context now carries initialization details only when initialization is explicitly selected.

## Trigger

The original launch contributor reconstructed a .NET solution from project titles, free-form
notes, and context summaries. It selected application templates and defaults such as a target
framework, test framework, and directory layout. This was a hidden product-design decision in
the process runtime, and it made an unrelated phrase in a process request capable of changing a
runtime-owned setup plan.

## Responsibility split

| Responsibility | Owner | Boundary | Test seam |
| --- | --- | --- | --- |
| Select and approve product topology | Architecture and slice templates / agents | Managed architecture and slice artifacts | Template and agent-output tests |
| Declare which upstream artifact a driver consumes | Process template | Generic driver activation binding | Loader validation tests |
| Carry exact parent artifact identity to a child launch | Generic Workbench/Runtime bridge | `ParentRequiredArtifactBindings` | Parent-context unit tests |
| Parse and validate a .NET bootstrap document | .NET process driver | `DotNetBootstrapDecisionParser` | Parser tests without process runtime |
| Ground explicit relative paths under product root | .NET process driver | `DotNetProcessLaunchContractFactory` | Factory path-safety tests |
| Create solution and project files | Existing runtime-owned .NET setup executor | Existing command boundary | Executor tests with fake command host |

## Decision

`dotnet-development-slice.slice-architecture-check` emits a second, bounded artifact named
`dotnet-bootstrap-decision`. It confirms the accepted architecture for the writable slice and
contains one fenced `dotnet.bootstrap-decision/v1` JSON object. It is not an inferred design and
must report contradictions instead of silently changing the architecture.

`dotnet-solution-setup` declares an opaque input binding for that exact artifact. The generic
template loader validates only binding shape; the generic parent-artifact bridge serializes only
the source step, expectation key, and managed ref. The .NET driver then reads only that bound
artifact and creates a launch contract from explicit values. No generic process type knows the
schema, app template, framework, test framework, or layout.

The decision uses the Builder/Factory pattern because a runtime setup plan has many required
fields and path-safety validation. A plain source-text parser was rejected: it hides product
design inside an implementation driver and cannot prove which agent decision supplied a value.

## Guardrails

- No legacy inference or silent defaults are retained.
- Product-relative paths must not be absolute or escape `ProductRoot`.
- A missing, malformed, ambiguous, or schema-mismatched bound artifact fails clearly before any
  setup command is executed.
- Template options are composed into the existing validated `dotnet new` command specification;
  the generic workspace command does not gain application-template knowledge.
- Root process templates no longer activate a .NET launch contract merely to spread inferred
  variables across unrelated steps.

## Testability contract

- Parser tests cover exactly-one JSON block, schema rejection, missing fields, and path escape.
- Factory tests cover a non-default topology without source prose, and prove source prose cannot
  affect the result.
- Generic parent-context tests prove source step, expectation key, and ref survive child launch
  preparation.
- Loader tests reject empty or duplicate opaque binding declarations.
- Runtime-owned setup tests prove explicit template options reach `workspace_dotnet_new`.
- A composition smoke proves the registered driver receives the bound artifact through the normal
  subprocess launch path.
