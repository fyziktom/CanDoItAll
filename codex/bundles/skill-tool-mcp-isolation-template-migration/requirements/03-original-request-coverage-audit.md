# Original Request Coverage Audit

| Original request item | Coverage decision | Bundle location | Gap closed in this review |
| --- | --- | --- | --- |
| Better isolation and templating of skills, tools, and MCPs. | Covered. | `architecture/01-target-solution.md`, SB01-SB12 | Added checkpoint gates to prevent isolated projects from becoming new monoliths. |
| Move tools/skills/MCPs out of MAF wrapper into own projects with abstraction and implementation. | Covered. | SB01-SB04, SB08 | Added dependency rules and quality guardrails. |
| Use `Templates/` folder for skill information. | Covered. | SB06, `templates/01-template-pack-design.md` | Extended to explicit template/seed hardening in SB07. |
| Tools and MCPs can be internal or external. | Covered. | SB02, SB04 | Added detailed external failure categories and setup-test diagnostics. |
| External tools need generic calls for Python, executable, HTTP, etc. | Covered. | SB02 | Added bounded process/http result requirements and diagnostic fields. |
| Internal tools need own implementation project. | Covered. | SB02 | Added structured folders, narrow interfaces, and file-size guardrails. |
| MCP can run internally with app or externally. | Covered. | SB04 | Added lifecycle owner and cleanup failure diagnostics. |
| Template setup for external MCP/tool connections. | Covered. | SB06, SB10 | Added typed setup-test result contract and UI/API rules. |
| Structured folders grouping related parts. | Covered. | SB02-SB04 | Added quality guardrails requiring split by domain and no overgrown files. |
| Proper tests for loading and call mechanisms. | Covered. | `inventories/02-test-inventory.md`, SB02-SB04, SB06-SB11 | Added unhappy-path diagnostics and performance scan requirements. |
| External Tools/MCPs testable during setup. | Covered. | SB04, SB10 | Added exact MCP start/list-tools failure states and UI response requirements. |
| Deep analysis of current architecture and reconnect places. | Covered. | `analysis/01-current-state.md`, `architecture/02-reconnection-map.md`, `analysis/03-codeanalytics-and-performance-review.md` | Added Codeanalytics snapshot and performance findings. |
| First create new implementations and harden/refactor before reconnecting. | Covered after review. | SB02-SB05, SB08-SB09 | Added SB05 and SB09 hardening checkpoints; moved MAF reconnection to SB08. |
| Use xlsx for detailed checklist and plan. | Covered. | `outputs/skill-tool-mcp-isolation-template-migration/skill-tool-mcp-isolation-plan.xlsx` | Workbook will be regenerated with 12 phases and error/hardening sheets. |
| Split tests into unit, integration, and e2e. | Covered. | `inventories/02-test-inventory.md`, `plan/01-phase-plan.md`, workbook | Added checkpoint-specific diagnostics/performance proof. |
| Preserve all functionality. | Covered. | SB06-SB12 | Added no-fallback and parity gates before cleanup. |
| Use naming standards from AI ecosystem where compatible. | Covered. | `requirements/02-naming-and-compatibility-standards.md`, SB01 | No change needed; compatibility remains primary. |
| Analyze exception/error states and avoid generic messages. | Covered after review. | `architecture/03-error-and-diagnostics-model.md`, SB01-SB11 | Added structured error model and failure inventories. |
| Limit or forbid skills/tools/MCPs per agent, process, workflow, and UI without stringly runtime logic. | Covered after access-policy review. | `architecture/05-capability-access-policy.md`, `inventories/04-capability-access-policy-test-inventory.md`, R15, SB01-SB12 | Added typed access policy, common exposure descriptors, effective capability set, template/UI conversion rules, and suppression diagnostics. |
| Improve short/open subbundles. | Covered after review. | SB02-SB12 | Added detailed acceptance criteria and checkpoint subbundles. |
| Prepare bundle only, no implementation. | Covered. | README, reviews, final validation | Production implementation remains untouched. |
