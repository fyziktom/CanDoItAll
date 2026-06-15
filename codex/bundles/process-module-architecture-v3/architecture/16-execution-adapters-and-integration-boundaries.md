# Execution Adapters And Integration Boundaries

## Design Intent

Workflow execution, single-agent execution, agent collaboration, handoff flows, scheduler-triggered starts, project/workbench integration, and plugin integration are adapters and strategies. Core/runtime see only generic execution kinds, strategy IDs, envelopes, diagnostics, and artifact references.

This protects the generic architecture from domain and provider-specific APIs.

## Adapter Types

| Adapter | Purpose | Runtime sees |
| --- | --- | --- |
| Workflow strategy adapter | Execute a workflow-backed step. | Strategy ID, result envelope, artifact refs, diagnostics. |
| Single-agent strategy adapter | Execute one agent role/session. | Strategy envelope and restricted diagnostic refs. |
| Agent-group/collaboration adapter | Coordinate multiple agents. | One normalized strategy result plus manager signals. |
| Handoff adapter | Execute handoff-capable flow through an adapter. | Generic handoff execution kind and strategy ID. |
| Scheduler-trigger adapter | Start process from scheduler/external trigger. | Run start request and correlation metadata. |
| Project/workbench adapter | Connect process runs to project/workbench context. | Generic context facets and artifact refs. |
| Plugin adapter | Expose external capability through driver/strategy contract. | Capability tag, strategy result, diagnostics. |

## Adapter Result Envelope

Adapters return the same normalized strategy envelope shape:

- strategy ID and version,
- execution kind,
- idempotency key,
- produced artifact refs,
- requested artifact refs,
- manager signals,
- branch decision request if applicable,
- restricted diagnostic refs,
- user-safe summary,
- telemetry summary,
- result hash.

No adapter mutates runtime state directly.

## Diagnostics And Restricted Evidence

Adapter diagnostics are classified:

- user-safe summary,
- restricted raw transcript/reference,
- sensitivity,
- suggested incident classification,
- retry/resume safety,
- idempotency classification,
- relevant artifact refs.

The manager decides how diagnostics become incidents.

## Integration Boundary Rules

- Core defines execution kinds only.
- Builder binds strategy IDs and adapter strategy metadata.
- Runtime schedules and validates state transitions.
- Dispatcher invokes strategy interface.
- Adapter talks to workflow/agent/handoff/scheduler/project/plugin infrastructure.
- Manager interprets adapter results and decides recovery/escalation.

## Scheduler Trigger Start

Scheduler starts are application-layer inputs:

1. Scheduler adapter receives trigger.
2. Adapter creates run start request with trigger correlation.
3. Application authorizes and invokes builder.
4. Builder compiles plan.
5. Runtime starts run from persisted plan.

Scheduler cannot create runtime state directly.

## Project/Workbench Integration

Project/workbench integration provides context facets:

- project ID,
- workspace scope,
- allowed mutation paths,
- relevant artifacts,
- user/team context,
- display links.

These facets are inputs to builder, manager, and strategies through generic context contracts. Project-specific rules do not enter core/runtime.

## Invariants

- Adapter implementation references do not appear in Core/Runtime generic contracts.
- Strategy result envelopes are the only execution result path.
- Adapter raw output is restricted evidence unless classified user-safe.
- Runtime does not call workflow/agent/handoff/scheduler/project/plugin APIs directly.
- Concrete adapter failures become strategy fault envelopes and manager incidents.

## Failure Behavior

| Failure | Required response |
| --- | --- |
| Adapter API unavailable | Strategy fault envelope and manager incident. |
| Adapter produces unauthorized artifact | Policy denial and artifact ledger rejection. |
| Adapter returns unsafe raw output | Store restricted evidence and emit sanitized summary only. |
| Scheduler duplicate trigger | Idempotent run start or duplicate-trigger incident. |
| Project/workbench context missing | Builder failure or manager incident depending on when detected. |

## Test Implications

- Adapter contract tests prove result envelopes and restricted diagnostics.
- Runtime tests use fake strategy implementations, not concrete adapters.
- Driver/adapter integration tests prove no generic runtime changes for concrete adapters.
- Scheduler duplicate-trigger tests prove idempotency.
- Project/workbench tests prove context facets stay outside core/runtime.
