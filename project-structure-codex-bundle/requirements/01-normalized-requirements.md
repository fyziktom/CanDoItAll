# Normalized Requirements

## Core Requirements
- `R01`: Execute the bundle in the documented order and do not skip prerequisite gates.
- `R02`: Preserve mapped behavior from `02_FEATURE_PRESERVATION_MAP.md`.
- `R03`: Use plain JavaScript for the renderer-side hot path.
- `R04`: Keep typed domain and persistence logic in C#.
- `R05`: Use Playwright MCP, screenshots, and targeted tests for UI-visible proof.
- `R06`: Reopen work instead of carrying known failures downstream.

## Functional Requirements
- `R07`: Overlay and floating-window interaction must not leak into scene pan, zoom, selection, or context-menu behavior.
- `R08`: Hot-path viewport and window movement must avoid server and DB chatter until commit or idle.
- `R09`: Node move and simple mutation flows must avoid unnecessary repeated persistence and full surface reloads.
- `R10`: The scene renderer must move toward retained, culled, patch-based updates.
- `R11`: Browser regression coverage and performance instrumentation must become explicit and reusable.

## Cross-Surface Requirements
- `R12`: Shared canvas changes must preserve PromptFactory behavior.
- `R13`: Shared canvas changes must preserve Sandbox-visible behavior or document a concrete follow-up.
- `R14`: Export, preview, modal, selection, and toolbar flows listed in the feature map remain intact.

## Closure Rule
- Every executed subbundle must end in `Completed` or `Blocked`.
- `Partially solved` is allowed only in raw-note closure during active execution and must create a follow-up path before final closure.
