# Structured Input

## Core Objective

- Preserve the phase07 closure evidence and reopen lanes for the new local process MCP and its install-discoverability workflow.

## Hard Constraints

- Do not claim closure if the MCP duplicated process-domain logic or skipped the standard reinstall/config workflow.
- Keep the current-session restart requirement explicit.
- Do not invent UI proof for a non-visual phase.

## Source Artifacts

- Use the phase07 subbundles, new MCP project files, installer and reinstall scripts, generated config files, install manifest, and root execution report as the authoritative evidence set.

## Input Coverage Signals

- Raw note `N11` required a simple MCP server for processes and their definitions.
- Raw note `N12` required reinstall-script coverage, skill sync, install proof, and explicit restart guidance.

## Dependency And Sequencing Signals

- The root process-management bundle may not return to `Completed` until phase07 proof exists and this repair bundle is generated and validated.

## Validation Expectations

- Require release build proof, focused unit tests, focused integration and stdio proof, reinstall/install proof, config inspection, manifest inspection, skill-sync inspection, and completed-stage bundle validation.

## UI Validation Strategy

- `N/A` because phase07 is non-visual.

## Browser Validation Analytics

- `N/A` because phase07 is non-visual.

## Working Assumptions

- The restart requirement affects only live tool discovery in the current Codex session.
- The generated config files and manifest represent the installed local truth after reinstall.

## Primary Risks

- Install-script drift, process-domain duplication inside the MCP, or future config drift would justify reopening a repair lane.
