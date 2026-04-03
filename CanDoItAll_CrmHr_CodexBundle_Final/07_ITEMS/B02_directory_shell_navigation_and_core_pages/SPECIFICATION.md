# Specification

## Objective

Add the CRM / HR shell entry, root pages, route structure, summary dashboard, directory workspace, and BaseLib-first page composition without using canvas components.

## Scope

- Add CRM / HR entry to shell navigation and nested route matching.
- Create the module home page and all route shells.
- Use BaseLib page scaffolds, summary tiles, secondary tabs, and list/detail patterns.
- Create a usable directory page shell even before advanced relationship features land.
- Add page-level smoke and component tests for route loading and basic create/edit flows.

## Services and entities involved

**Services**

- `PartyDirectoryService`

**Entities / concepts**

- `Party`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **DIR-03** As an account manager, I can search the directory by name, role, tag, status, email, phone, and company so I can find the right record quickly.
- **DIR-14** As a project manager, I can see a party activity timeline so I understand the latest interactions, assignments, and changes before acting.
- **DIR-15** As an executive assistant, I can open a party directly from global search so the directory behaves as a first-class application surface.
- **CRM-18** As a business director, I can see account summaries and open opportunities from the CRM/HR home screen so I do not have to reconstruct pipeline from projects.
- **CRM-19** As a sales assistant, I can search across opportunities and accounts from one CRM workspace so navigation is fast.
- **HR-35** As a project manager, I can view allocated people and units per project from the HR side so staffing ownership is bidirectional.
- **AI-08** As a delivery lead, I can search agents in the same directory and assignment flows as people so blended staffing stays unified.
- **X-01** As a platform owner, I can add CRM / HR as a shell module with nested routes so it feels native inside CanDoItAll.
- **X-04** As a UI architect, I can implement the module with BaseLib and standard HTML only so the CRM/HR experience stays outside canvas concerns.
- **X-13** As a platform owner, I can keep core screens performant with large directories so the module scales beyond toy usage.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- Navigating to `/crm-hr` and the child routes works without shell errors.
- The Directory page can create and edit a basic party record.
- All CRM/HR pages use BaseLib-first layouts and do not import canvas libraries.
- Playwright smoke flow proves navigation, save, and reload persistence.
