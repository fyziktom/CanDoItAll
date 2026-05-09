# Normalized Requirements

| Requirement | Description | Acceptance |
| --- | --- | --- |
| `REQ-001` | Fix the Architecture Beta Mermaid failure. | `docs/architecture-beta.md` uses GitHub-safe Mermaid syntax and no `architecture-beta` block. |
| `REQ-002` | Update technical docs for current API-first architecture. | Docs describe `/api`, `/api/project-structure`, projects, processes, agents, API access, and validation at a practical level. |
| `REQ-003` | Stop presenting suppressed MCPs as active. | README/docs describe Processes and ProjectStructure MCP as retired/suppressed and route users to API skills/Swagger. |
| `REQ-004` | Add less-technical customer/wiki content. | A new customer-facing doc explains why CanDoItAll helps, how it works, and what enterprise users need to prepare. |
| `REQ-005` | Add four audience-specific enterprise infographics. | Four generated images exist under `docs/images` and are referenced from the customer-facing doc. |
| `REQ-006` | Include process operations concepts. | Docs explain Plan, Execute, Validate, Audit; escalations; observation manager; HR/agent matching; and audit evidence. |
| `REQ-007` | Include Economy ledger direction carefully. | Docs describe `CanDoItAll.Economy` as adjacent external work for private ledger, traceability, cash flow, simulation, and agent economic boundaries. |
| `REQ-008` | Validate documentation changes. | Bundle validators, `git diff --check`, and stale-reference searches are recorded in the execution report. |
