# Target Solution

## End State

- AgentFramework owns the editable AI-agent catalog for the target profile, including instructions, skills, provider configuration, capabilities, and runtime workspace bindings.
- CRM-HR renders AI agents as a business-facing directory projection over AgentFramework-owned technical agents and never becomes an independent registry.
- The target SQLite profile contains a serious project for a Blazor SSR basic-units-converter application, with feature blocks, delivery phases, role expectations, process attachments, and durable run artifacts.
- Delivery, review, QA, and release work is carried by real CanDoItAll agents created through AgentFramework and backed by OpenAI provider configuration.
- QA-capable agents can reach `playwright-local-mcp`, capture screenshots, and reason over screenshot evidence instead of relying only on text logs.
- Project structure, process runtime, and durable output folders stay aligned so a human can inspect what happened from the workbench instead of reconstructing it from hidden storage.

## Canonical Ownership Boundaries

- `CanDoItAll.Modules.AgentFramework` owns technical-agent identity, editable instructions, capability assignments, skill attachments, and runtime provider configuration.
- `CanDoItAll.Modules.CrmHr` owns business-directory presentation, human-readable classification, and workflow entry points for AI resources, but it must call through the AgentFramework bridge for AI-agent edits and projections.
- `CanDoItAll.Modules.Processes` owns template-driven execution logic, role resolution, artifact handoff, approval steps, and runtime dispatch.
- `CanDoItAll.Modules.Workbench` owns cross-module visibility of project structure, run progress, and durable output nodes.
- Scenario- or project-provisioning helpers may seed serious projects, but they must compose reusable templates and reusable agent definitions instead of hardcoding showcase-only narrative or role naming.

## Required Architectural Repairs

- Collapse the split between the legacy organization scope and the active profile-id organization scope so CRM-HR and the Agents page resolve the same agent catalog.
- Preserve existing CRM rows and bindings only as projections that can be repaired or migrated into the canonical AgentFramework scope; do not keep a dual-source model alive.
- Move serious project provisioning away from showcase-flavored orchestration and into template-driven process and role composition wherever reusable templates already exist.
- Strengthen seeded or provisioned delivery agents with explicit C# and Blazor instructions, OpenAI provider configuration, Playwright access where appropriate, and screenshot-aware QA expectations.
- When the live run proves that oversized files are blocking maintainability, split them along module boundaries instead of layering more branching logic into already large files.

## Non-Goals

- Do not introduce a second long-term synchronization engine between CRM-HR and AgentFramework.
- Do not preserve showcase naming, showcase-only project text, or showcase-only folders in the serious units-converter project.
- Do not bypass the process-template system with one-off hardcoded orchestration if the same behavior belongs in reusable templates.
