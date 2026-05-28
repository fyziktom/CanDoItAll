# Current State

## Output Grounding

- `BuildProjectStructureGroundingSummary` focuses on the target node, selected process node, descendants, and planning siblings at the target node's immediate parent.
- In the live Tetris project, the selected target is nested under the `Main app` delivery branch. The output folder is under a separate top-level `Main architecture` branch, so it is not part of the focus set.
- The downstream prompt only applies external output root rules when the grounding summary contains a mapped external target. Because the output folder was omitted, the first process step recorded the managed workspace output root as the product root.

## Manager Chat

- The Processes page manager tab resolves manager agent id from run manager id, override id, exact manager name, or a single manager-like option.
- The live run has `ManagerAgentId = null` and `ManagerAgentName = Default process manager`.
- The database has multiple manager-like options, so the local resolver returns null and the UI displays the connection error.
- The live dashboard service already resolves by configured manager, then run assignments, then a scored fallback.

## Run Folder Projection

- `ProcessProjectionContributor` derives output folder nodes by calling `ResolveManagedOutputDirectoryPath` on every artifact record path.
- The current implementation returns each artifact file's immediate directory.
- For the live run, that creates folders for `Components/Layout`, `wwwroot`, test directories, and date-based tool receipt directories instead of a small set of run-level workspace folders.
