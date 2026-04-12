# Library Foundation And Preview Models

## Status

- `Completed`

## Objective

- Add the package and service foundation required for the new template browser, including template-browser view-models, preview content resolution, and explicit import helpers.

## Covered Inputs

- Use `MermaidJS.Blazor`, `Markdig`, and `JsonViewer.Blazor`.
- Reuse current template-pack files under `Templates\Processes`.
- Do not invent a new persisted roles or artifacts library model.

## Prerequisites

- none

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\TestApplicationBootstrap.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateCatalogService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateProjectionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplatePackResourceModels.cs
- C:\repositories\CanDoItAll\Templates\Processes

## Deliverables

- Package references for the requested preview libraries.
- Mermaid service registration in the real app and component-test bootstrap.
- Strongly typed browser models for list items, preview payloads, and import actions.
- Service methods that resolve process, role, and artifact previews from the template pack and derive markdown or json sidecars safely.
- Explicit helpers for process import, role draft creation, and artifact expectation creation.

## Dependency Impact

- Every downstream UI phase depends on this subbundle because the modal cannot render or import anything without stable preview and import models.
- Weak proof here would cause UI work to fail late with missing package services, wrong sidecar paths, or ambiguous import semantics.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add the requested package references in the correct projects.
2. Register MermaidJS services in the runtime app and component-test bootstrap.
3. Introduce strongly typed models for library categories, list items, preview payloads, and import targets.
4. Extend the existing template catalog seam with browser-oriented list and preview methods.
5. Add explicit helper methods that map role and artifact templates into current editor models without silent fallback behavior.

## Scope Exceptions

- Do not build the modal UI in this phase.

## Do Not Do

- Do not create a new persisted entity for a global role or artifact library.
- Do not hard-code template metadata in UI components.
- Do not defer package or DI wiring to later UI phases.

## Acceptance Checklist

- Package integration compiles cleanly in the affected projects.
- Browser preview models can enumerate processes, roles, and artifacts from the real template pack.
- Artifact import helpers require an explicit target step.
- No fallback logic silently swallows missing sidecars or unknown template keys.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v:minimal`
- Targeted tests or service assertions that prove browser models resolve from `Templates\Processes`.
- Evidence that `Program.cs` and `TestApplicationBootstrap.cs` both register MermaidJS services.

## Browser Validation Logging

- N/A. This phase is not browser-visible.

## Progression Gate

- Downstream UI work may continue only after the solution builds and the preview service resolves real template data for all three categories.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Add the package and preview-model foundation for the process templates browser without building the modal UI yet.
```
