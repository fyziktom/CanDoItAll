# Structured Input

## Core Objective

- Ship a maintainable hierarchy feature that lets projects relate to other projects as parents and children, including multi-parent children, then make that hierarchy discoverable from `/projects` and actionable from `/projects/{id}/structure` without weakening the existing bundle workflow or closing on shallow proof.

## Hard Constraints

- Preserve strong typing and explicit failures. Invalid hierarchy operations must reject predictably instead of silently falling back.
- Keep the change small in architecture terms. Favor extending `ProjectsService`, typed models, and workbench sync instead of inventing unnecessary layers.
- Support many-to-many project hierarchy relationships and arbitrary depth traversal.
- Prevent self-parenting and cyclic parent chains so the hierarchy remains navigable and the workbench sync cannot recurse into nonsense.
- Keep the Projects page within the current component and CSS approach already used by the repo.
- Keep the project-structure canvas within the existing canvas/workbench system and action catalog.
- Capture real browser validation with Playwright MCP and screenshots before the bundle can close.
- Capture workflow analytics in `candoitall-skill-analytics`, turn them into repo skill changes, and make sure install/sync scripts ship those changes to other machines.

## Source Artifacts

- The raw user request preserved in `C:\repositories\CanDoItAll\project-hierarchy-bundle-1\inputs\00-original-request.md`
- The installed global skill files listed in `C:\repositories\CanDoItAll\project-hierarchy-bundle-1\inputs\01-source-artifacts.md`
- The repo-local skill pack under `C:\repositories\CanDoItAll\codex\skills`

## Validation Expectations

- Targeted integration tests for project hierarchy persistence, cycle prevention, and workbench projection.
- Targeted component tests for the Projects page and structure canvas.
- A clean build and targeted test pass after all subbundles land.
- Headed Playwright MCP proof on `/projects` and `/projects/{id}/structure`.
- Screenshot review for large-screen desktop and a narrower follow-up width where layout changes matter.
- Execution report rows populated for browser analytics, subbundle gates, and raw-note closure.
- Final bundle validation plus a skill-install sync check for the changed repo-managed skills.

## UI Validation Strategy

- Start UI validation in a large desktop viewport (`1600x1000` or equivalent maximized work area).
- Review screenshots against readability, spacing, alignment, clipping, modal layering, and whether hierarchy cues are obvious.
- Follow with a narrower width (`1280x900`) on the same routes after the desktop pass is stable.
- For modal and overlay flows, prove the open state itself and not just the trigger button.
- For new-tab canvas actions, verify the originating page stays intact and the related project route opens correctly.

## Browser Validation Analytics

- Subbundle 02 logs `/projects` hierarchy discovery and recursive subproject modal behavior.
- Subbundle 03 logs `/projects/{id}/structure` hierarchy nodes, ghost-parent presentation, reconnect/add flows, and new-tab actions.
- Subbundle 04 logs the final cross-surface regression pass that ties both routes together and closes the raw notes.
- The analytics review in `reviews/01-execution-report.md` must explicitly say whether screenshots, assertions, and route interactions were strong enough or whether a subbundle must reopen.

## Working Assumptions

- The requested "infinite tree structure" is implemented as a directed acyclic project hierarchy. Multiple parents are allowed, cycles are not.
- The Projects page can use a recursive drill-in modal or breadcrumb-backed modal reuse; the user does not need multiple overlapping modal shells if the recursive navigation is clear and preserves context.
- Each project's structure canvas surfaces the current project, its direct parents, its direct children, and the extra-parent context needed to explain multi-parent children. Arbitrary-depth traversal is provided through repeated navigation and new-tab opening, not by rendering the full transitive closure on one canvas.
- The existing system-managed `project:{id}` root node remains the anchor node for a project's own structure surface.

## Primary Risks

- A poor hierarchy schema choice can force broad refactors across projects, workbench sync, and tests.
- Multi-parent display can become visually confusing unless secondary-parent nodes are clearly subdued and route actions stay obvious.
- Recursive modal navigation can become stateful and brittle if it competes with the existing details/editor modal flows.
- The repo-local skill pack is incomplete relative to the workflow rules being used in this run; if not fixed, the next machine or next run will repeat the same process defect.
