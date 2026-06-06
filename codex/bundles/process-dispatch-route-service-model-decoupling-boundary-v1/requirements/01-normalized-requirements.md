# Normalized Requirements

| ID | Requirement | Acceptance proof |
| --- | --- | --- |
| REQ-001 | Continue incremental dispatch isolation; do not start Process Core. | Source scan: no `CanDoItAll.Processes.Core` project/path/type references. |
| REQ-002 | Preserve all existing dispatch route behavior. | Focused unit + integration route tests; exact route order matrix. |
| REQ-003 | Introduce route-owned module-local snapshots for candidate, claim, execution context, route outcome, and direct-agent execution outcome. | Source assertions and route model tests. |
| REQ-004 | Remove dispatcher nested model aliases from route handlers and route facets after adapter migration. | Source scan forbids `using DispatchCandidate = ProcessRunAutomationDispatchService.*` in route handler/facet files. |
| REQ-005 | Split `ProcessDispatchRouteServices` into narrow route service implementations or adapters. | Source scan: no all-facet class implements unrelated route facets. |
| REQ-006 | Make route handler factory consume a facet set or explicit narrow services, not one all-facet service. | Source assertion on `ProcessDispatchRouteHandlerFactory.cs`. |
| REQ-007 | Keep side effects explicit and categorized. | Side-effect matrix for all route stages. |
| REQ-008 | Keep future driver preparation documentation-only. | No production driver API tokens; driver-readiness document updated. |
| REQ-009 | Do not touch UI or create mobile/small/medium proof. | Git diff/source scan excludes UI/media/viewport proof. |
| REQ-010 | Every subbundle must have its own execution-report row; no collapsed `SB001-SB128` row. | Execution report guard. |
