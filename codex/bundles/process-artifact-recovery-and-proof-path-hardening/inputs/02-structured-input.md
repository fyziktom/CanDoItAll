# Structured Input

## Notes

| Note | Normalized meaning |
| --- | --- |
| `N001` | The live process is blocked and reports missing artifacts or proof. |
| `N002` | Rerunning a downstream step cannot repair an artifact that should be produced upstream. |
| `N003` | Missing upstream artifacts should cause a request to the previous producing step or process manager. |
| `N004` | After the missing artifact exists, the blocked downstream step should retry. |
| `N005` | The process core must remain generic. |

## Constraints

- Keep process runtime generic.
- Use project structure, process step definitions, skills, and agent instructions for domain-specific acceptance.
- Do not hardcode Tetris, Blazor, canvas, or app-specific behavior in process core.
