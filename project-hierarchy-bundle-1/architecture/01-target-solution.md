# Target Solution

## Domain And Persistence

- Add a dedicated project-hierarchy relation model in the Projects module instead of trying to overload the existing single-parent workbench node model.
- Keep `Project` itself flat. The hierarchy should be expressed through a typed relation record such as `ProjectHierarchyLink` with unique `ParentProjectId` and `ChildProjectId`.
- Extend `ProjectsService` with typed hierarchy query and mutation methods for:
- listing projects with hierarchy metadata
- reading a project's direct parents and direct children
- adding a relation
- reconnecting a relation
- removing or replacing a relation when required
- validating self-link and cycle rules before persistence
- Preserve existing search-index and activity-stream behavior for ordinary project save/delete flows.

## Projects Page

- Extend project summary/view models so `/projects` has direct access to parent and child counts plus lightweight related-project cards.
- Add hierarchy-aware filter state to the page instead of forcing all hierarchy discovery into the modal.
- Keep the existing card/modal workflow, but add a dedicated "Subprojects" affordance per card and modal content that supports recursive drill-in and shows multi-parent state.
- Reuse existing project actions from hierarchy cards wherever possible so the page stays coherent with the current UI vocabulary.

## Structure Canvas

- Treat the project hierarchy table as the truth for project-to-project relations.
- Keep the system-managed `project:{id}` root node for the current project.
- Project direct child relations into system-managed related-project nodes and links in `SyncGraphAsync`.
- Project direct parents of the current project into the same surface as context nodes.
- For child projects that also belong to other parents, project those extra parents as contextual related-project nodes with metadata or subtype hints that let the graph adapter render them in a subdued style.
- Do not try to render the entire transitive closure on one canvas. The canvas should show the current project's immediate hierarchy neighborhood clearly, then let the user open related project canvases in new tabs for deeper traversal.
- Extend the action catalog and structure page quick-action flow so related-project nodes can:
- open their project structure in a new tab
- be added as new subproject relations
- be reconnected to another parent

## Visual Contract

- Projects page hierarchy cues must feel native to the existing card system, not like a separate admin console bolted onto it.
- The subproject modal must make recursion and multi-parent state obvious without stacking unreadable overlays.
- Secondary-parent nodes on the canvas must be visibly de-emphasized through palette, alpha, dashed border, or similar styling, while still remaining discoverable and openable.

## Proof Strategy

- Use integration tests to prove relation persistence, cycle rejection, and workbench projection.
- Use component tests to prove Projects page hierarchy affordances and structure-canvas rendering/actions.
- Use real Playwright MCP validation on both hierarchy routes, capture screenshots, and answer the explicit visual questions before closure.
- Use the execution-report analytics rows as a required artifact, not optional documentation.

## Skill-Pack Closure

- Add the missing validator skills to the repo-managed skill pack.
- Update the bundle workflow and related repo skill files with any lessons learned from this run.
- Update the repo install script so it copies every repo-managed custom skill needed by the repaired workflow.
- Keep `tools/Reinstall-CanDoItAllMcps.ps1` aligned with the repo skill-pack layout so a reinstall syncs the updated skills automatically.
