# CanDoItAll CRM / HR — Final Codex bundle

This bundle is an execution-grade implementation package for adding a **merged CRM / HR module** into the CanDoItAll app.

The bundle assumes the new module is named **`CanDoItAll.Modules.CrmHr`**, while its internal domain is built around a **unified Party model** so the same real-world actor can be reused across CRM, HR, Projects, Workbench, Resources, Validation, Test Lab, and AI-agent workflows.

## Why this bundle exists

The repository already contains a **project-local participant / CRM-lite concept** inside Workbench. That is no longer enough for the requested scope. This bundle upgrades the concept into a real shared module without breaking the existing project structure model.

The design explicitly supports all of these at the same time:

- people
- companies
- organization units / delivery units
- contractors and freelancers
- customers, partners, and vendors
- candidates
- AI agents

## Bundle contents

- `00_INPUTS/` — original request and repo context
- `01_ANALYSIS/` — verified current-state findings and CRM/HR touchpoints in CanDoItAll
- `02_REQUIREMENTS/` — enterprise user-story catalog, CanDoItAll mapping, business-director review, scope rules
- `03_ARCHITECTURE/` — target architecture, data model, route/UI design, integrations, privacy model
- `04_PLAN/` — phase plan, mermaid gantt, dependency map, risks, and phase gates
- `05_TRACEABILITY/` — story catalog, traceability matrix, manifest, and bundle validator
- `06_SHARED_PROMPTS/` — master implementation and validation prompts plus Playwright/screenshot protocols
- `07_ITEMS/` — implementation subbundles with detailed instructions, file references, ASCII layouts, and acceptance criteria
- `08_QA/` — final QA inspector review and sign-off

## Bundle counts

- Enterprise CRM/HR user stories: **120**
- Implementation subbundles: **13**
- Execution waves: **5**
- Required documents per subbundle: **9**

## Mandatory design rules

1. **One unified Party root.** Do not split CRM people and HR people into separate registries.
2. **BaseLib only for UI.** Do not import canvas-specific components into the CRM/HR module.
3. **Workbench participants stay alive.** They become project-side projections or references to central parties, not an abandoned concept.
4. **Search and activity are first-class.** CRM/HR entities must appear in global search and timeline history.
5. **Sensitive data is handled deliberately.** Confidential HR notes and broad search indexing are not the same thing.
6. **UI work is not complete without screenshots and semantic review.**
7. **Project integration is not optional.** The module must solve customer / partner / delivery-unit / person / AI-agent assignment inside Projects and Workbench.

## Recommended reading order

1. `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`
2. `02_REQUIREMENTS/SCOPE_AND_NON_FUNCTIONAL_DECISIONS.md`
3. `02_REQUIREMENTS/ENTERPRISE_USER_STORY_CATALOG.md`
4. `01_ANALYSIS/CRM_HR_TOUCHPOINT_MAP.md`
5. `04_PLAN/IMPLEMENTATION_SEQUENCE.md`
6. `07_ITEMS/` in dependency order

## Final quality bar

The bundle is complete only when:

- all user stories are traceable,
- all subbundle acceptance criteria pass,
- automated tests pass at the appropriate level,
- Playwright validation is executed,
- screenshots are captured and semantically reviewed,
- the project/workbench integration truly uses the new unified Party model.
