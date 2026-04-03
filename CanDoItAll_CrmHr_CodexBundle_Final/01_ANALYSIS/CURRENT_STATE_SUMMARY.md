# Current state summary

This document captures the verified repository state that matters for the requested CRM / HR implementation.

## Repository snapshot

- Repository root analyzed: `CanDoItAll-canvas-toolbox`
- Main web startup: `src/CanDoItAll.Web/Program.cs`
- Module assembly registry: `src/CanDoItAll.Web/Composition/ModuleAssemblies.cs`
- Shell navigation source: `src/CanDoItAll.Web/Composition/ShellNavigation.cs`
- Shared EF model composition: `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- Existing validation layers: unit, component, integration, and Playwright projects already exist

## Key verified findings

- There is **no dedicated CRM or HR module** in the current solution. Startup currently registers Security, Workspace, Projects, Workbench, Resources, Prompts, Factory, Validation, Test Lab, Activity, and Automation.
- The shell has **no CRM / HR navigation entry**. New navigation and route composition must be added in the web layer.
- The app already has a reusable **global search index** through `ISearchIndexService` and `SearchDocument`. New CRM/HR entities should plug into this instead of creating a second search mechanism.
- The app already has a reusable **activity timeline** through `IActivityStream` and `ActivityService`. CRM/HR changes should write into that timeline rather than introducing another audit-like feed for general usage.
- `ProjectsService` already supports project CRUD, hierarchy, and search/activity integration, but it does **not** currently carry customer, partner, delivery-unit, or account-manager context.
- `ResourcesService` already gives the app a typed registry for repositories, folders, files, web links, SSH, secrets, scripts, and prompt links. This is a strong reuse point for ownership and maintainer relationships.
- `WorkspaceService` already persists provider profiles and health for OpenAI / Ollama variants. This is the strongest existing reuse point for AI-agent configuration.
- `Workbench` already contains a **project-local people model**:
  - `ProjectObjectType.Participant`
  - `ProjectParticipantMetadata`
  - participant kinds `Hr`, `TeamBlock`, `TeamSection`, `Freelancer`, `Partner`, `AiAgent`
  - meeting metadata with participant ids
  - work-item metadata with participant assignee
- Workbench already exposes create-catalog entries for **HR**, **team block**, **team section**, **freelancer**, **partner**, and **AI agent**. That proves the product already needs actor assignment inside projects, but the current storage is still project-local.
- The current participant handling is therefore **CRM-lite / HR-lite inside Workbench**, not a full shared enterprise module.
- `ProjectWorkbenchMetadata` already uses structured JSON metadata. That makes it realistic to add central party references without reworking the whole workbench persistence strategy.
- The app already has strong Playwright support through `tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs` and smoke tests in `AppSmokeTests.cs`. This is the right validation base for the requested screenshot-backed QA.

## Current UI implications

- Existing list/detail CRUD screens already use BaseLib well, especially:
  - `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
  - `src/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
  - `src/CanDoItAll.Modules.Activity/Pages/ActivityPage.razor`
- `docs/ui-shared-components/README.md` explicitly warns that BaseLib is suitable for **simple CRUD and layout surfaces**, while dialog-like overlays and advanced grids are intentionally limited.
- That matches the user instruction: the CRM/HR module should be implemented with **BaseLib and standard HTML/Tailwind**, not canvas tool windows.

## Strong reuse opportunities

- Reuse `ISearchIndexService` for party, account, opportunity, workforce, recruiting, and agent search documents.
- Reuse `IActivityStream` for visible timeline entries and pair it with a deeper CRM/HR audit table for sensitive or technical events.
- Reuse `ProjectsService` and `ProjectWorkbenchService` instead of duplicating project identity or structure concepts.
- Reuse `Workspace` provider profiles for AI-agent runtime bindings.
- Reuse `Resources` for linked operational artifacts instead of attaching everything directly to party records.
- Reuse existing test infrastructure across all layers.

## Gaps that must be solved explicitly

- There is no shared actor registry that unifies **person / organization / delivery unit / AI agent**.
- There is no cross-project reusable representation of customer, partner, employee, contractor, or candidate.
- There is no CRM opportunity pipeline, interaction journal, or next-action model.
- There is no HR workforce, staffing, recruitment, onboarding, or offboarding model.
- There is no project-level assignment model that can link parties to project summary, workbench nodes, validation ownership, or test ownership in a reusable way.
- Sensitive HR data and general searchable CRM data are not yet separated.
- Existing Workbench participant data must be migrated carefully so project structure remains useful during and after the CRM/HR rollout.

## Existing tests worth extending

- `tests/CanDoItAll.Tests.Components/ProjectsPageTests.cs`
- `tests/CanDoItAll.Tests.Components/ResourcesPageTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectsServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Bottom line

The repository already contains the **signals** that CRM/HR belongs in CanDoItAll:
projects, participants, meetings, AI agents, resources, validation, tests, activity, and search.

What it does **not** yet contain is the **shared enterprise-grade actor model** that lets all of those surfaces talk about the same customer, company, delivery unit, person, or agent. That is the core gap this bundle closes.
