# Sandbox solution shape

## Proposed project

- `src/CanDoItAll.Components.WebGlSandbox`

## Proposed startup shape

The dedicated sandbox should follow the existing `Components.Sandbox` hosting style, but it should live in a separate project because the concept needs a focused route space and proof matrix.

## Proposed primary route

- `/webgl/process-workbench`

Optional query parameters:

- `template=<key>`
- `view=overview|roles|dependencies|branching|focus`
- `camera=perspective` remains tolerated for backward-compatible route state, but the sandbox now enforces perspective authoring after execution refinement.

## Sandbox responsibilities

- load representative templates,
- host the generic WebGL workbench component,
- keep edits in sandbox-only state,
- show selection and last-command context,
- support reset and screenshot/export proof flows.

## What should not live here

- production process persistence,
- a replacement for `ProcessWorkspace`,
- deep product chrome unrelated to the concept,
- module-wide side effects outside the sandbox.
