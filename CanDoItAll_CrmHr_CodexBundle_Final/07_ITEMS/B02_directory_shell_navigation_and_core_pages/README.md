# B02 — Directory shell, navigation, routes, and core BaseLib pages

## Purpose

Add the CRM / HR shell entry, root pages, route structure, summary dashboard, directory workspace, and BaseLib-first page composition without using canvas components.

## Dependencies

B01

## Main stories covered

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

## Main routes

- `/crm-hr`
- `/crm-hr/directory`
- `/crm-hr/crm`
- `/crm-hr/workforce`
- `/crm-hr/recruiting`
- `/crm-hr/agents`
- `/crm-hr/assignments`

## Done when

- Navigating to `/crm-hr` and the child routes works without shell errors.
- The Directory page can create and edit a basic party record.
- All CRM/HR pages use BaseLib-first layouts and do not import canvas libraries.
- Playwright smoke flow proves navigation, save, and reload persistence.
