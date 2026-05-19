# 02-folder-file-link-actions

## Status

- Status: `Completed`

## Objective

- Make folder, file, repository, and link nodes resolve local open actions and GitHub/GitLab recognition from their typed metadata.

## Success Criteria

- Folder-style nodes can store a folder path and expose Explorer open actions when the path is trusted and exists.
- Local-drive file nodes expose Explorer file-location actions when path metadata is trusted and exists.
- Repository and link nodes recognize GitHub and GitLab URLs through metadata, aliases, or display helpers.

## Covered Inputs

- `N002`: Explorer opens the wrong location for folder/file nodes.
- `N003`: folder node must allow selecting or storing a folder path and opening it.
- `N004`: repository and link nodes must recognize GitHub and GitLab links.
- `R003`
- `R004`
- `R005`

## Prerequisites

- `01-01-runtime-launch-foundation` completed or explicitly documents that action menu wiring is safe to reuse.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureLocalFileOpener.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.RichDefinitions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCreateRequestComposer.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureNodeEditor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureNodeDescriptor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs

## Deliverables

- Local opener resolves folder and file paths from typed metadata in addition to managed storage references.
- Folder create/edit metadata remains round-trippable and visible.
- File location behavior distinguishes folders from files.
- GitHub/GitLab URL recognition is represented in tests and user-visible metadata or labels.

## Dependency Impact

- `03-03-agent-catalog-and-ui-proof` depends on these exact supported metadata keys before updating agent instructions.
- Playwright proof depends on the folder/file/link create dialogs and quick actions being stable.

## Validation Depth

- Critical UI and host foundation with resolver tests, component tests, and browser-visible proof.

## Implementation Steps

1. Extend local path resolution to supported folder/file/repository/infrastructure metadata while keeping guard checks.
2. Add tests for folder direct open, file select/open location, blocked executable file handling, and missing path feedback.
3. Add GitHub/GitLab URL recognition helpers and tests for repository and link nodes.
4. Validate create/edit fields for local folder and deployment folder nodes.
5. Update execution report rows.

## Scope Exceptions

- Unsupported or unsafe host paths remain blocked with visible messages.
- GitHub/GitLab recognition does not include API calls, authentication, or remote clone inspection.

## Do Not Do

- Do not allow Explorer to execute script-like files.
- Do not silently fall back to home path when a configured path is invalid.
- Do not add a new object type unless existing folder-style node types cannot satisfy the request.

## Acceptance Checklist

- [x] Folder node path opens the folder location.
- [x] Local file path opens file location with Explorer select semantics.
- [x] Missing/blocked paths show failure messages.
- [x] Repository and link URL recognition handles GitHub and GitLab.
- [x] Existing managed asset and IPFS open behaviors continue to pass.

## Proof Required

- Targeted component/unit tests for local opener, page actions, and URL recognition.
- Execution report gate row.
- Browser screenshot of folder/file/repository/link action surfaces.

## Browser Validation Logging

- Route: `/workbench/projects/{projectId}/structure`.
- Viewport: large desktop first pass.
- Actions: create/select local folder, file path, repository URL, and link URL nodes; open action surface; screenshot.
- Screenshot: folder/file/link proof paths recorded in `reviews/01-execution-report.md`.
- Review questions: folder/file action labels visible, dialogs not clipped, GitHub/GitLab recognition readable without layout overlap.

## Progression Gate

- Passed. Local-open and host-recognition tests passed; Playwright MCP captured folder, file, GitHub, and GitLab proof screenshots. The execution report documents that automated proof does not launch Explorer or UAC windows.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
