# Structured Input

## Core Objective

- Add the missing workflow API commands needed for development-time control and create a matching Codex skill for the workflow API.

## Success Criteria

- Workflow API exposes justified lifecycle and import/export commands.
- Targeted workflow API tests pass.
- `codex/skills/candoitall-api-workflows/SKILL.md` exists and follows the existing API skill pattern.
- `tools/Reinstall-CanDoItAllMcps.ps1` syncs the new skill into `%USERPROFILE%\.codex\skills`.

## Hard Constraints

- Strongly typed workflow contracts only.
- No new Workflow MCP server.
- No silent fallback behavior for invalid workflow definitions, imports, or runtime backends.
- No broad UI work.

## Allowed Side Effects

- Workflow API models, service contracts, service implementations, endpoint routes, integration tests, repo skill files, and bundle files.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`

## Input Coverage Signals

- N001: detailed workflow API review and missing commands.
- N002: workflow API skill based on project-structure/processes API skill structure.
- N003: OpenAI docs validation for GPT-5.5 skill compatibility.
- N004: reinstall with MCP reinstall script.
- N005: setup skill on this environment before user restart.

## Dependency And Sequencing Signals

- API command list must land before the skill can accurately document workflow routes.
- Skill must exist under `codex\skills` before reinstall/local setup proof.

## Validation Expectations

- Targeted workflow API integration tests.
- Skill file existence and frontmatter inspection.
- Reinstall/local skill path proof.
- Prepared and completed bundle validators.

## Evidence Contract

- Record API test/build commands in `reviews/01-execution-report.md`.
- Record official OpenAI docs skill validation source in `reviews/01-execution-report.md`.
- Record reinstall command output and local skill path proof in `reviews/01-execution-report.md`.

## UI Validation Strategy

- N/A. This bundle changes API and skill/setup artifacts only.

## Browser Validation Analytics

- Record N/A rows for all subbundles because no browser-visible UI changes are in scope.

## Working Assumptions

- Workflow control during development means the HTTP API control plane.
- Lifecycle and import/export are the missing commands with clear value; run observation and cancellation already exist.

## Primary Risks

- Lifecycle command implementation might accidentally lose graph/runtime policy if it does not preserve the current definition payload.
- Import/export could leak persistence records if not modeled as an API envelope.
- Reinstall proof could be slow because the script also publishes MCP artifacts.
