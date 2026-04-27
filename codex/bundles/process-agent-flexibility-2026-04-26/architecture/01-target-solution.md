# Target Solution

## Target Shape

- `ProcessRunAutomationDispatchService` emits only platform-neutral execution rules: step objective, handoffs, expected artifacts, governed evidence rules, upstream artifact gates, recovery directives, and status contract.
- Technology-specific guidance is carried by seeded agents through instruction assets and capability assignments.
- Process templates model domain-specific workflows. A software-delivery process can involve .NET/JS specialists; a business-plan process can involve business, finance, and marketing specialists without inheriting coding assumptions.
- Tests validate prompt shape and seed/template compatibility before expensive process execution.

## Boundaries

- The dispatcher should not know how to create a calculator, Blazor app, JavaScript app, business plan, marketing plan, or financial model.
- The dispatcher can still enforce generic contracts: required artifacts, durable evidence, explicit blocked/failed/completed outcome, and no silent validation failure.
- The seed catalog can include technology and domain depth because agents are the correct specialization boundary.
- Process-template JSON/markdown remains the process-definition source; do not introduce a hard-coded process definition path in C#.

## Validation Strategy

- Atomic: inspect generated prompts and seeded catalog in unit/integration tests.
- Template: load and project the new non-code business-plan process.
- PostgreSQL: run focused process tests under PostgreSQL using existing support.
- Real-agent: attempt one real scenario only after deterministic tests pass; record a real blocker if credentials/provider state prevents it.
