# Normalized Requirements

| ID | Requirement | Acceptance signal | Owner |
| --- | --- | --- | --- |
| R01 | Isolate skills, tools, and MCPs from MAF into dedicated abstraction and implementation projects. | New projects compile, expose typed interfaces, and MAF consumes interfaces instead of nested config/builders. | SB01-SB09 |
| R02 | Keep existing functionality and compatibility. | Existing seeded agents retain capability assignments; existing runtime tool names and behavior are unchanged unless explicitly versioned. | SB06-SB11 |
| R03 | Store capability definitions in `Templates/` instead of hardcoded seed code. | `Templates/Capabilities` defines skills, tools, MCPs, metadata, policy, setup test data, and stable IDs; seed builder materializes from templates. | SB01, SB06, SB07 |
| R04 | Support internal and external tools. | Internal tool descriptors bind to implementation keys; external tool descriptors support process/http transports, JSON schemas, timeouts, bindings, approval, and setup tests. | SB01, SB02 |
| R05 | Support internal and external MCP servers. | MCP descriptors cover internal hosted, local stdio, and remote HTTP transports with lifecycle ownership, allowed tools, secret bindings, approval, and list-tools setup tests. | SB01, SB04 |
| R06 | Add tool setup UI/API and harden MCP setup testing. | Blazor setup wizard and API can create, edit, test, and inspect tools and MCPs without raw JSON for normal cases. | SB10 |
| R07 | Keep structured folders in new projects. | Implementation folders are grouped by domain, for example `Workspace`, `DotNet`, `Documents`, `Images`, `Processes`, `External`, and `ProviderNative`. | SB02-SB04 |
| R08 | Make loading and call mechanisms easy to test and mock. | Loader, resolver, invoker, lifecycle manager, and MAF adapter all have interface-driven tests with fake implementations. | SB01-SB05, SB08-SB09 |
| R09 | Split validation into unit, integration, and e2e tests. | Workbook and subbundle proof require explicit unit, integration, component, and Playwright coverage where applicable. | All |
| R10 | Use AI ecosystem naming conventions where compatible. | Runtime tool names follow MCP/OpenAI/Anthropic-compatible ASCII naming; capability keys remain kebab-case; typed constants/generation avoid magic strings. | SB01, SB12 |
| R11 | Preserve security posture. | Raw secrets are rejected, command execution is policy-gated, logs mask sensitive values, and setup tests produce actionable errors. | SB02, SB04, SB10 |
| R12 | Reconnect only after hardening. | SB08 is blocked until SB01-SB07 prove contracts, implementation projects, hardening gates, template loader, seed parity, and setup-test services. | SB08 |
| R13 | Use structured diagnostics for all capability load, setup, and call failures. | Failures carry a typed category, capability key, capability kind, template path or implementation key, correlation ID, bounded detail, masked raw output, and repair guidance. | SB01-SB11 |
| R14 | Force hardening/refactoring/optimization before downstream phases. | SB05, SB07, and SB09 block progression until file size, dependency direction, cycle, diagnostics, testability, and focused performance gates pass or record accepted risks. | SB05, SB07, SB09 |
| R15 | Support generic capability access restrictions for agents, processes, workflows, and UI. | A typed access policy can deny or require skills, tools, MCP servers, and MCP tools by common descriptors, tags, operation classifications, or stable keys without hardcoded string switches; denial never grants missing capabilities; MAF/UI/templates consume the same evaluator and emit actionable suppression diagnostics. | SB01-SB12 |
