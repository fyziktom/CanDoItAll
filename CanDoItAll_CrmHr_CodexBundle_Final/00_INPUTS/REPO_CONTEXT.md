# Repository context used for this bundle

## Input artifact

- Uploaded repository zip: `/mnt/data/CanDoItAll-canvas-toolbox.zip`
- Extracted analysis root: `/mnt/data/work/CanDoItAll-canvas-toolbox`

## Verified repository characteristics

- Solution entry point: `CanDoItAll.slnx`
- .NET SDK pin in `global.json`: `10.0.200`
- Main web entry point: `src/CanDoItAll.Web/Program.cs`
- Existing module assembly registration: `src/CanDoItAll.Web/Composition/ModuleAssemblies.cs`
- Shell navigation: `src/CanDoItAll.Web/Composition/ShellNavigation.cs`
- EF model assembly scanning: `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`

## Current first-class modules

- Dashboard — route `/` — CanDoItAll.Web
- Projects — route `/projects` — CanDoItAll.Modules.Projects
- Structure canvas — route `/projects/{ProjectId}/structure` — CanDoItAll.Modules.Workbench
- Project calendar — route `/projects/{ProjectId}/calendar` — CanDoItAll.Modules.Workbench
- Resources — route `/resources` — CanDoItAll.Modules.Resources
- Prompt Gallery — route `/prompt-gallery` — CanDoItAll.Modules.Prompts
- Prompt Factory — route `/prompt-factory` — CanDoItAll.Modules.Factory
- Validation Center — route `/validation` — CanDoItAll.Modules.Validation
- Test Lab — route `/test-lab` — CanDoItAll.Modules.TestLab
- Activity — route `/activity` — CanDoItAll.Modules.Activity
- Automation — route `/automation` — CanDoItAll.Modules.Automation
- Settings — route `/settings` — CanDoItAll.Modules.Workspace / Security

## Important architectural facts

- The app is a modular Blazor Web App using Interactive Server rendering.
- Existing non-canvas CRUD surfaces already use `CanDoItAll.Components.BaseLib`.
- Workbench already contains lightweight participant and AI-agent concepts, but only inside project structure metadata.
- Global search and activity stream already exist and should be reused instead of replaced.
- Existing automated validation already includes unit, component, integration, and Playwright projects.
- The user explicitly asked that the new CRM/HR module **must not use canvas-related UI components**.

## Core design assumption locked by this bundle

The new module is implemented as **`CanDoItAll.Modules.CrmHr`** and is centered on a **unified Party model**. CRM, HR, and AI-agent handling become different views and profiles on top of the same root identities.

## Scope posture

This bundle targets a **serious project-delivery enterprise CRM/HR surface** for CanDoItAll. It intentionally covers:

- shared directory and relationship management,
- CRM account/contact/interaction/opportunity flows,
- HR workforce/staffing/recruitment flows,
- AI agents as first-class actors,
- deep project/workbench integration,
- privacy, audit, search, activity, and testing.

This bundle intentionally does **not** try to turn CanDoItAll into a payroll or full ERP suite.
