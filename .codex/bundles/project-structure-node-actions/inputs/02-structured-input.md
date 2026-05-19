# Structured Input

## Core Objective

- Make project-structure runtime, folder/file, repository, and link nodes behave as executable or openable nodes from the canvas and expose enough catalog guidance for agents to create those nodes correctly.

## Success Criteria

- Runtime-capable nodes offer both normal and administrator launch actions in the double-click quick-action dialog and context menu whenever a trusted launch plan resolves.
- PowerShell launches use the configured node command and the configured path or working directory when present.
- Docker runtime nodes and Python runtime nodes are covered by the same launch surface.
- Folder nodes can store a concrete folder path and offer an Explorer open action.
- File nodes that point to a local drive path can offer Open in File Explorer behavior without falling back to the user's home path.
- Repository and link nodes recognize GitHub and GitLab URLs in metadata, aliases, labels, or display hints.
- Agent project-structure tools tell agents how to add links, runtime scripts, folders, and file nodes, including metadata keys and runtime-node examples.

## Hard Constraints

- Preserve workspace path safety checks for launching PowerShell and Explorer.
- Do not run arbitrary script files directly through Explorer open actions.
- Do not break existing managed asset, IPFS, workflow, process, or project hierarchy actions.
- Validate with Playwright MCP and screenshots.

## Allowed Side Effects

- Edit workbench project-structure services, metadata helpers, catalog definitions, agent contracts/guidance, component/unit tests, and bundle evidence files.
- Add focused tests and Playwright evidence artifacts for the changed surfaces.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`

## Input Coverage Signals

- `N001`: runtime nodes must start processes, including normal/admin choices and PowerShell command/folder handling.
- `N002`: folders and file locations must open in Explorer at the configured path instead of home.
- `N003`: folder node creation must allow selecting or typing a folder path.
- `N004`: repository and link nodes must recognize GitHub and GitLab links.
- `N005`: agent tools must explain how to add links, runtime scripts, folders, and files of all types.
- `N006`: proof must include Playwright MCP and screenshots.

## Dependency And Sequencing Signals

- Runtime launch resolution is a critical foundation because UI actions and agent actionCapabilities depend on it.
- Local path/open capability is a critical foundation because folder/file/repository actions and agent guidance depend on it.
- Agent catalog guidance should land after the actual supported metadata and aliases are known.

## Validation Expectations

- Targeted component and unit tests for runtime launcher, local opener, action catalog, catalog aliases/guidance, and page action rendering.
- Build or broader test run if targeted tests touch shared project-structure types.
- Playwright MCP large-screen proof for the canvas create/select/double-click action dialogs.
- Narrower viewport check when dialog or toolbox layout is affected.

## Evidence Contract

- Command results recorded in `reviews/01-execution-report.md`.
- Browser validation analytics rows with route, viewport, Playwright MCP actions, screenshot paths, and pass/fail result.
- Host-visible behavior documented through safe resolver tests and, where feasible, direct Windows shell launch smoke evidence.

## UI Validation Strategy

- Start with a large desktop viewport on the project structure page.
- Exercise create dialogs for runtime script, Python runtime, Docker node, local folder, file/link/repository nodes.
- Select or double-click nodes and capture the open quick-action dialog or visible inspector actions.
- Check that action labels are readable, not clipped, and not hidden behind floating windows.
- Run a narrower viewport pass for dialog readability if the UI changed.

## Browser Validation Analytics

- Log one row per executed subbundle in `reviews/01-execution-report.md`.
- Include Playwright MCP route, viewport, actions, assertions, screenshot path, and result.

## Working Assumptions

- Folder-node support can use the existing Repository local-folder node and infrastructure deployment-folder node if the UI and agent catalog label and explain it clearly.
- Docker runtime nodes can be represented by infrastructure docker nodes when they include explicit command metadata.
- Existing workspace guard policy should continue to decide which local paths are trusted.

## Primary Risks

- UAC elevation cannot be fully automated without user consent, so direct elevated launch proof may be limited to launch-plan resolution and non-admin host smoke.
- Absolute paths outside the workspace may be intentionally blocked by current guard policy unless a project-structure local path is explicitly trusted.
- Blazor canvas double-click behavior may require Playwright browser proof in addition to component tests.
