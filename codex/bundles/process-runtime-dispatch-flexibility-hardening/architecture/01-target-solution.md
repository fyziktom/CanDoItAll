# Target Solution

## Target Shape

The process runtime should remain a domain-neutral orchestration layer. Generic scheduling, claims, immutable plan validation, runtime state transitions, and queue lifecycle stay in Processes. Step execution dispatch behavior, prompt fragment composition, completion-evidence policy, provider/tool invocation, subprocess adapter policy, and driver-specific recovery semantics belong to selected process drivers. AgentFramework, project-structure, .NET, browser screenshot, and software-delivery details must be optional driver or contributor behavior registered by composition, not private logic in the runtime or dispatcher core.

## Layering Rules

- `CanDoItAll.Processes.Abstractions`, `Contracts`, `Core`, `Builder`, and `Runtime` stay free of AgentFramework, Workbench, ProjectStructure, Blazor, .NET template, and UI screenshot concepts.
- `CanDoItAll.Processes.Application` owns launch orchestration, dispatch scheduling/claim orchestration, assignment records, dispatch queue contracts, and branch signal application. It may resolve and invoke drivers through ports, but it must not contain AgentFramework/MAF prompt, completion evidence, provider, tool, or recovery policy.
- `CanDoItAll.Processes.Drivers.Abstractions` owns stable driver contracts. New ports for prompt composition, completion-evidence validation, driver step execution dispatch, and driver-specific recovery policy belong here when the generic runtime/application layer needs to call them.
- `CanDoItAll.Processes.Drivers.Standard` remains descriptor and adapter strategy wiring unless SB01 proves it should own domain-neutral standard implementation.
- AgentFramework/MAF-specific execution should live below Processes in the dependency tree, preferably in an MAF-owned process driver implementation that references `CanDoItAll.Processes.Drivers.Abstractions`. Do not create a `src/Processes/*` project that references MAF. If composition constraints block the preferred project, stop and repair the bundle instead of adding a reverse dependency.
- Workbench owns project-structure launch enrichment and subprocess launch coordination because those features depend on project nodes and Workbench services.

## Proposed Service Slices

- Driver catalog and factory resolution: catalog provider, strategy resolver, descriptor registration.
- Executor resolution and assignment repair: agent readiness, role matching, override handling, operation contract validation.
- Driver step execution dispatch: selected driver handles provider/tool execution policy, prompt strategy selection, completion evidence policy, driver-specific recovery/resupply, and result conversion behind typed driver ports.
- Prompt composition: generic process brief contract, driver-owned prompt strategy, prompt fragment contributors, model/provider prompt options.
- AgentFramework/MAF driver orchestration: load assignment through process ports, resolve subprocess state, invoke agent runtime, validate structured output, load execution detail, validate completion evidence, convert to strategy result.
- Subprocess lifecycle service: detect active child runs, stopped child outcomes, launch coordinator output, deferred dispatch, synthesized completion.
- Completion evidence policy: driver-owned managed artifact materialization, required path checks, required receipt checks, required file content checks, product root inspection, grounding of path-like references.
- Runtime observation and telemetry readers: execution observation mapping and usage telemetry parsing.
- Claim recovery and cancellation: recovery coordinator, observer, reconciler, worker, cancellation observer.
- Generic dispatcher orchestration: claim lifecycle, branch router delegation, retry budget guardrails, projection catchup, queue scheduling, and invocation of driver-owned step execution dispatch ports.

## Explicit Non-Goals For Implementation

- Do not rewrite process runtime semantics.
- Do not remove software-delivery, .NET, screenshot, subprocess, or product mutation behavior.
- Do not replace existing tests with weaker broad smoke tests.
- Do not move UI components unless a dependency cleanup requires it.
- Do not add abstractions that have no test or boundary value.

## Expected End State

- The main integration file is reduced to composition or removed entirely.
- Domain-specific prompt text and launch variables are discoverable through contributor/strategy names.
- Runtime dispatch can execute generic enterprise tasks without software-delivery guidance.
- AgentFramework execution remains fully supported but is no longer the hidden shape of the whole runtime.
- MAF/AgentFramework process support is below the Processes boundary: it implements process driver contracts and is wired by composition; Processes projects do not reference it.
- Critical behavior has direct tests and artifact-backed proof.
