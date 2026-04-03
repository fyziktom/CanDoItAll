# Implementation prompt

Implement **B02 — Directory shell, navigation, routes, and core BaseLib pages** for CanDoItAll.

## Bundle goal

Add the CRM / HR shell entry, root pages, route structure, summary dashboard, directory workspace, and BaseLib-first page composition without using canvas components.

## Hard rules

- follow `03_ARCHITECTURE/*` and `02_REQUIREMENTS/SCOPE_AND_NON_FUNCTIONAL_DECISIONS.md`
- keep UI in BaseLib / Razor / HTML only
- do not introduce canvas components
- preserve backward compatibility for existing project/workbench flows where relevant
- add or update tests listed in `FILE_REFERENCES.md`
- add screenshot evidence requirements from `SCREENSHOT_REQUIREMENTS.md`

## Implementation steps

1. Inspect all files in `FILE_REFERENCES.md`.
2. Implement the data model / service changes required for this bundle.
3. Implement the route or UI changes required for this bundle.
4. Wire search/activity/integration seams if this bundle requires them.
5. Add automated tests at the correct level.
6. Execute browser validation and capture screenshots.
7. Write a concise evidence note summarizing code changes, tests, and screenshots.

## Bundle-specific targets

- Add CRM / HR entry to shell navigation and nested route matching.
- Create the module home page and all route shells.
- Use BaseLib page scaffolds, summary tiles, secondary tabs, and list/detail patterns.
- Create a usable directory page shell even before advanced relationship features land.
- Add page-level smoke and component tests for route loading and basic create/edit flows.

## Stories that must be satisfied in this bundle

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

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
