# Assumptions And Risks

## Assumptions

- The user wants the buttons for all launchable runtime nodes, not only for `dotnet watch`.
- `projectPath` and `workingDirectory` values are workspace-relative paths that can be resolved against the current workspace root.
- Reusing PowerShell as the outer launcher is acceptable even when the underlying command is `python`, `conda`, or `dotnet`.

## Risks

- path resolution mistakes can open PowerShell in the wrong directory or fail only after the shell opens
- deriving dotnet commands in the page would duplicate runtime knowledge and make the feature hard to maintain
- trying to "best effort" missing launch metadata would hide configuration mistakes instead of surfacing them

## Mitigation

- centralize launch-plan resolution in a dedicated workbench service
- keep the selection panel responsible only for rendering buttons and surfacing explicit success or failure feedback
- limit button visibility to nodes whose typed metadata can produce a deterministic launch plan
