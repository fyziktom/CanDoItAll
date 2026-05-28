# Source Artifacts

| Artifact | Type | Location | Notes |
| --- | --- | --- | --- |
| Original request | Text | `bundle://inputs/00-original-request.md` | Raw user request preserved. |
| Main repository | Git worktree | `repo://.` | Existing CanDoItAll solution and projects. |
| Components repository | Git worktree | `C:/repositories/CanDoItAll.Components` | User-created repository for moved component projects. |
| Current solution | Solution XML | `repo://CanDoItAll.slnx` | Main slnx currently includes moved component projects and Space3D projects. |
| Tailwind workspace | CSS/npm workspace | `repo://Tailwind` | Currently emits one BaseLib output from the main repository. |
| Component project folders | Project sources | `repo://src/CanDoItAll.Components.*` | Eight project folders must move, two component-related projects must remain. |
