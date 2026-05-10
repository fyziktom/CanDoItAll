# Final Architecture Review And Production Blockers

## Status

- `Implemented app path accepted`
- `Production durability/persistence follow-up remains open`

## Scope

- Reviewed the completed wrapper, runtime, catalog/API, Workflows page, canvas editor, process workflow executor, and web API/navigation integration.
- Checked the implementation against the Microsoft Agent Framework durable workflow guidance and local MAF source references.

## Accepted Architecture

| Area | Result | Notes |
| --- | --- | --- |
| MAF wrapper boundary | Accepted | Product models/contracts live in `AgentFramework.Models/Core`; MAF compile/run mapping remains in `AgentFramework.Maf`. |
| Workflow runtime manager | Accepted for preview/test/non-durable use | Product run snapshots, events, artifacts, and external requests are represented above MAF and are observable through app APIs/UI. |
| Catalog/settings/components/API | Accepted | `/api/workflows` owns workflow catalog, settings, component library, validation, test-runs, run status/events, artifacts, and external request responses. |
| Agents module UI | Accepted | `/agents/workflows` is a separate workflow page inside the Agents module and does not merge workflows into technical-agent tabs. |
| Canvas editor | Accepted | Canvas state maps to typed workflow definitions, nodes, edges, ports, LLM Call Components, validation, preview run, and persisted node layout. |
| Workflow LLM provider ownership | Accepted after repair | MAF does not impose a separate provider catalog; workflow LLM Call Components now consume existing CanDoItAll agent provider profiles/options and validate provider purpose/capabilities. |
| Process role integration | Accepted | Processes remain above workflows and agents; workflow is a typed executor option with explicit run links and process artifact projection. |
| Web API integration | Accepted after fix | Process run detail now serializes workflow run links, and scoped assignment resolution preserves workflow definition/version ids. |

## Production Blockers

| Blocker | Status | Required next action |
| --- | --- | --- |
| DurableTask/DTS backend | Open | Add the approved `Microsoft.Agents.AI.DurableTask` package and configure MAF durable workflows with `ConfigureDurableOptions` when agents and workflows are hosted together, or record a different reviewed host boundary. |
| Azure Functions generated endpoints | Open by decision | Keep generated run/status/RequestPort/MCP endpoints unexposed until product authorization, audit, and deployment ownership are approved. If used, wrap or explicitly govern them behind CanDoItAll APIs. |
| Persistent workflow product storage | Open | Replace singleton in-memory workflow catalog/run stores with a persistent store for definitions, versions, settings, components, runs, events, artifacts, and external requests before calling the feature production durable. |

## Durable Article Alignment

- The implementation follows MAF guidance for treating workflows as first-class workflow graphs and keeps DurableTask/DTS as the production direction.
- MAF provider adapters are treated as runtime integration mechanisms, not as a replacement for CanDoItAll provider governance. Existing agent provider profiles remain the catalog for workflow LLM calls.
- The implementation does not yet call `ConfigureDurableOptions` because the repo has no DurableTask/Azure Functions hosting package reference and no selected DTS/emulator configuration.
- The in-process backend is intentionally limited to tests, previews, local development, and short non-durable runs. It must not be promoted as the durable production runtime.

## Validation Summary

- Full solution build passed with 0 warnings/errors.
- Workflow unit tests passed 16/16.
- Workflow component tests passed 3/3.
- Workflow API integration tests passed 4/4.
- Provider registry repair validation passed: workflow catalog unit tests 9/9, workflow component tests 3/3, workflow API integration tests 5/5, and focused Core/AgentFramework module/Web builds passed with 0 warnings/errors.
- Process workflow executor integration tests passed 5/5.
- Process launch planning regression passed 15/15.
- Browser screenshots cover Workflows page, canvas authoring/test run, workflow executor selection, and process workflow run ledger.
- Performance scan found no sync-over-async, blocking waits, `Task.Run`, `Thread.Sleep`, regex, culture-sensitive case conversion, or string comparison issues requiring code changes.

## Gate Result

- The implemented application path is accepted for further product iteration and non-durable preview/testing.
- Production closure is intentionally not claimed until DurableTask/DTS hosting and persistent workflow storage are implemented and revalidated.

## Re-entry Review Addendum

- Date: 2026-05-10.
- Accepted repair: MAF checkpoint persistence helper moved from `CanDoItAll.AgentFramework.Core` to `CanDoItAll.AgentFramework.Maf`; Core is back to product contracts/provider-neutral orchestration and no longer references `Microsoft.Agents.AI.Workflows`.
- Accepted repair: workflow catalog save now snapshots incoming graph collections to prevent mutable caller-owned lists from mutating the stored canonical workflow definition.
- Regression guards: `WorkflowArchitectureBoundaryTests.AgentFrameworkCoreDoesNotReferenceMafWorkflowPackage` and `WorkflowCatalogTests.CatalogSnapshotsDefinitionGraphOnSave`.
- Remaining architecture concern: the in-process workflow backend still binds all node kinds as generic payload-forwarding executors, and `WorkflowRuntimeManager` handles human-input nodes with a coarse pre-backend pause. This remains acceptable only for preview/test/non-durable proof and must be reopened before production semantic execution.
