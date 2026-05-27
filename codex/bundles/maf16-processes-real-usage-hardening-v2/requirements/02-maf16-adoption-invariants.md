# MAF 1.6 Adoption Invariants

1. MAF package upgrade must be traceable to tests that exercise actual agent execution.
2. MAF tool invocation must always pass through CanDoItAll policy, including local MCP, hosted MCP, function tools, browser tools, shell/script tools, project-structure tools, and process tools.
3. Structured output/finalizer instructions should use the cleanest available MAF mechanism, preferably message injection where suitable, rather than only prompt concatenation.
4. Agent/session files should be evaluated for process artifact storage so tool receipts and current-run artifacts are first-class session evidence.
5. Workflow evaluation expected outputs should be used for deterministic process template and workflow-backed-role regression tests.
6. A2A v1 upgrade must have at least a compile-level and smoke-level proof or be explicitly disabled/guarded.
7. OpenTelemetry behavior must be tested for double wrapping and missing traces.
