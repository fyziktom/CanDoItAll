# Assumptions And Risks

## Working Assumptions

- Runtime execution stays Windows PowerShell based for this request because the current runtime launcher is Windows-only and the user specifically called out PowerShell.
- Folder-node support can use existing local-folder and deployment-folder node types if they become clear and functional from the UI and agent catalog.
- Local drive file/folder open must continue to pass through `IWorkspacePathAccessGuard`; unsupported paths should produce visible guidance instead of silently opening home.
- GitHub/GitLab recognition is a metadata and catalog/presentation behavior, not a full remote Git provider integration.

## Critical Path Risks

- If runtime launch resolution is too narrow, downstream UI and agent actionCapabilities will show no useful action even if catalog nodes exist.
- If local path resolution accepts unsafe paths, Explorer and PowerShell actions could expose arbitrary host execution or sensitive folders.
- If agent catalog guidance is vague, agents may keep creating generic notes or blocks instead of typed runtime/folder/file/link nodes.

## Validation Risks

- UAC elevation cannot be fully automated through Playwright; closure may need resolver assertions plus a documented host smoke for normal launch and a safe explanation for admin proof.
- Browser screenshots prove UI affordances and dialogs, but not the launched external PowerShell or Explorer window unless host-level capture is also possible.
- Component tests can validate action rendering, but Playwright MCP is still required for the real Blazor route and overlays.

## Reopen Triggers

- Reopen `01-runtime-launch-foundation` if any runtime-capable node lacks both normal and admin actions when its command/path metadata is valid.
- Reopen `02-folder-file-link-actions` if Explorer opens the wrong folder, no folder node can store a path, or file location actions are missing for local-drive files.
- Reopen `03-agent-catalog-and-ui-proof` if the catalog does not expose concrete create instructions and aliases for runtime scripts, folders, files, links, GitHub, or GitLab.
- Reopen the relevant earlier phase if Playwright screenshots show clipped dialogs, missing actions, stale labels, or broken canvas interaction.
