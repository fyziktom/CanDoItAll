# C# Testability Plan

## Seams

| Seam | Test double | Required proof |
|---|---|---|
| Executor contributions | Static contribution/factory | Catalog and invoker share exact descriptor; duplicate/missing implementation failure |
| Document conversion | Fake content converter | Source ingestion and document node delegate exactly once; limits/diagnostics preserved |
| File/spreadsheet/image operations | Fake typed operation service | Settings mapping, failures, output, receipts/policy separation |
| Command recipes | Fake command service and approval policy | Allow-list, approval, cancellation, masked logging, no arbitrary command |
| Launch service | Fake catalog/runtime/backend catalog | Active/preview policy, typed origins, backend rejection, caller parity |
| Runtime lifecycle | Controllable backend, fake store, `TimeProvider` | Running persisted before completion, incremental progress, terminal/cancel/failure |
| Usage persistence/projection | In-memory observation store, fixed pricing/time | Model grouping, known/unknown cost, dedupe, duration, executor usage |
| Settings renderer registry | Static trusted/untrusted sources | Key/trust/version/component contract, explicit missing renderer, schema fallback |
| Schema-to-canvas codec | Schema fixtures | Defaults, every field kind, invalid JSON preservation, round trip |
| Process workflow driver | Fake workflow catalog/launch/runtime services | Explicit selection, typed origin/input/idempotency, typed-origin recovery, no duplicate, waiting/terminal mapping, unsupported output/artifact rejection |

## Test Layers

- Unit: contracts, registries, settings codecs, operation adapters, launch policy, lifecycle transitions, analytics arithmetic.
- Component: executor creation/editing, renderer diagnostics, plugin schemas, analytics panel.
- Integration: real DI catalog/invoker parity, plugin manifests, persistence migration/query, API totals, process/scheduler/project/agent paths.
- Browser: maximized `/agents/workflows` create/save/reload nodes and inspect provider/model/token/cost/duration analytics.

## Negative Tests

- Duplicate executor ID with a different descriptor.
- Runnable descriptor without implementation; implementation without descriptor.
- Plugin executor with dangling renderer key, unsupported schema, or untrusted component renderer.
- Invalid settings JSON remains visible and cannot be overwritten by defaults without explicit user action.
- Unsafe/raw command request is rejected before command-service invocation.
- Unknown pricing remains unknown rather than `$0`.
- Replayed observation does not double count.
- Requested durable backend absence fails instead of silently selecting InProcess.
- Process workflow selection ambiguity fails before launch.
- Same-workflow runs with non-process or mismatched assignment origin never count as recovery children.
- Externalized/truncated workflow output and process artifact contracts fail explicitly until a supported mapping contract exists.

## Replacement Of Shallow Guards

- Remove assertions that require partial executor files.
- Replace raw file-length gates with no-partial executor ownership plus direct collaborator tests.
- Update the project graph test to forbid Core → Runtime and prove active Abstractions contracts have consumers.
- Use production DI in plugin descriptor parity tests; do not fabricate descriptors.
