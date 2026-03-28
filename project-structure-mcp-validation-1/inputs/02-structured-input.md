# Structured Input

## Core Objective

- Validate the repaired CanDoItAll project-structure MCP on the live app by importing and reshaping the supplied XMind package into `CanDoItAll Main`, while proving the server can read, create, link, update, import, and report useful state without hanging or losing access.

## Hard Constraints

- Use `C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md` as the governing workflow.
- Use the supplied input package as the live validation source, not a synthetic example.
- Mutate the already existing `CanDoItAll Main` project in the live CanDoItAll Web app.
- Use richer project-structure semantics than flat task import whenever the source meaning clearly maps to project blocks, repositories, files, environments, infrastructure, or subprojects.
- Capture any MCP failures, missing capability, or weak proof inside this bundle and in the validation workspace instead of ignoring them.
- Validation is not complete until live proof exists for creation and retrieval of the transferred structure.

## Source Artifacts

- `C:\Users\lucys\OneDrive - TechnicInsider\Produkty\CanDoItAll\CanDoItAllInput`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\inputs\source-artifacts\CanDoItAllInput`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\inputs\source-artifacts\CanDoItAllInput.xmind`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\analysis\03-xmind-summary.json`
- `C:\repositories\CanDoItAll\project-structure-mcp-validation-1\analysis\04-xmind-outline.md`

## Input Coverage Signals

- `N001` Validate the MCP with the exact attached mindmap package because another Codex window previously reported it could not access data through the server.
- `N002` Analyze the mindmap content and map it into the richer CanDoItAll project-structure model instead of relying on flat generic nodes only.
- `N003` Transfer the source into the existing `CanDoItAll Main` live project, using subprojects for larger branches.
- `N004` Exercise the new MCP broadly enough to expose what still does not work and capture every defect into `project-structure-mcp-validation-1`.
- `N005` Prove live readback of the created structure, not only mutation success responses.
- `N006` Capture analytics data and checklist evidence so post-validation review can see what actually happened.

## Dependency And Sequencing Signals

- Source analysis and node-type mapping must finish before any live mutation because the user explicitly asked for semantically correct node choices.
- Validation workspace creation and project lease proof must finish before broad import, otherwise later mutation failures cannot be isolated cleanly.
- Live import and reshaping must finish before checklist and analytics closure, because those reports depend on the final created structure.
- If the live MCP exposes a defect during any earlier phase, repair or explicitly reopen that phase before continuing downstream.

## Validation Expectations

- List and read live projects through the MCP in this session.
- Create or link a validation workspace under `CanDoItAll Main`.
- Package the copied XMind folder into a valid `.xmind` archive and import it through the MCP.
- Create and read back richer typed nodes and subprojects derived from the source meaning.
- Capture live browser proof from the CanDoItAll Web UI for the resulting structure.
- Capture checklist and analytics evidence for the validation run.
- Record any missing MCP surface or broken behavior as an explicit defect with proof.

## UI Validation Strategy

- Use Playwright against the running CanDoItAll Web app after the live import and shaping steps.
- First validate the relevant project-structure routes at a large desktop viewport with screenshots and visual review.
- Then run a narrower-width follow-up on the same routes if the structure page layout shifts materially.
- Review screenshots for readability, clipping, alignment, hierarchy legibility, and whether the imported structure is actually visible and navigable.

## Browser Validation Analytics

- Record route, viewport, Playwright actions, assertions, screenshots, and pass or fail result per executed subbundle in `reviews/01-execution-report.md`.
- Record the live structure page for the validation workspace and at least one shaped subproject under `CanDoItAll Main`.
- Record whether browser proof matched MCP readback and whether any UI or data mismatch forced a reopen.

## Working Assumptions

- The running manager instance exposes the app at the local address already configured for the MCP.
- The current session can now call the project-structure MCP, even though another Codex window previously had stale access problems.
- `CanDoItAll Main` is the correct target root and is safe to extend with validation content.
- The supplied source folder is an unpacked XMind package and can be re-zipped into a valid `.xmind` archive for the import tool.

## Primary Risks

- The live MCP may still have stale-session or capability gaps that do not show up in unit tests.
- The XMind import tool currently creates generic container and task nodes, so richer semantic shaping may require explicit follow-up node creation and project linking.
- Analytics query exists in the HTTP API but may not be exposed as an MCP tool, which would be a surface-area defect for this validation.
- Creating too much flat structure directly under `CanDoItAll Main` would make the result noisy and reduce the value of the validation.
