# Current State

## Workspace Surface

- `ProcessWorkspace.razor` currently exposes `New definition` and `Seed development baseline` actions in the page header.
- The existing workspace already uses `ListDetailShell`, `Tabs`, and the shared modal component patterns, so the requested browser can stay aligned with BaseLib instead of introducing bespoke layout chrome.
- The workspace already owns the orchestration state for the current definition editor, selected run, and canvas windows.

## Template Data Surface

- `ProcessTemplateCatalogService` currently supports process list cards for the old toolbox and draft generation for role and step templates.
- `ProcessTemplateProjectionService` already materializes a process template into a real import envelope, which is the correct seam for `Add to my processes`.
- The template pack under `Templates\Processes` already contains markdown, json, mermaid, and resource sidecars that can drive the requested preview panel.

## Domain Limits That Affect Scope

- Roles are persisted as part of a `ProcessDefinitionEditorModel`, not as a standalone reusable entity.
- Artifact expectations are persisted under `ProcessStepEditorModel.ArtifactExpectations`, not under a standalone artifact library.
- Because of that model shape, role import can target the current definition editor directly, while artifact import must target a concrete step in the current editor.

## Existing Proof Surface

- `ProcessWorkspaceTests.cs` already validates authoring and canvas flows and is the right place for new component-level regression coverage.
- `AppSmokeTests.ProcessManagementBundle.cs` already exercises `/processes` end to end and is the right nearby Playwright proof to extend.
