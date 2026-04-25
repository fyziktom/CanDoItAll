# Assumptions And Risks

## Working Assumptions

- OverlayLib can reference BaseLib/Common UI primitives already used by `OverlayWindow`.
- Tree-specific project structure behavior can be represented with generic grouped toolbox items and expandable groups.
- Prompt factory preview behavior can remain page-owned if the generic toolbox provides a per-item secondary action or preview slot.
- WebGL role addition should use process role templates so the 3D scene stays semantically aligned with existing process definitions.

## Critical Path Risks

- Over-generalizing the toolbox could break project structure create actions, especially required input defaults and action IDs.
- Moving prompt factory markup could accidentally break tokenized component creation or preview placement.
- WebGL role addition could render in the data model but not appear in 3D if the adapter filters or lays out role nodes unexpectedly.
- Existing dirty worktree changes in WebGL chrome must be preserved and not reverted.

## Validation Risks

- Static builds alone will not prove the toolbox adds real canvas or WebGL nodes.
- Project structure routes depend on available local project data; validation may need the central project-structure MCP to choose a real project.
- The Web app may have existing NuGet warnings that should not be confused with regressions.
- Playwright screenshots must show the post-add state clearly enough to verify the visible node/block, not only the toolbox click.

## Reopen Triggers

- Reopen subbundle 01 if any host needs data not expressible by the generic toolbox models without custom markup escape hatches.
- Reopen subbundle 02 if project structure, process canvas, or prompt factory add flows fail after migration.
- Reopen subbundle 03 if WebGL can add a role in memory but the role model does not appear as a person node in 3D.
- Reopen subbundle 04 if Playwright cannot prove both a project structure block add and a WebGL role add.
