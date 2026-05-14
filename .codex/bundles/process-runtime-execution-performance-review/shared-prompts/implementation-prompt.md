# Implementation Prompt

Implement the runtime-start performance repair for `CanDoItAll.Modules.Processes`.

Constraints:

- Keep process logic generic.
- Preserve assignment precedence and existing lifecycle semantics.
- Do not add .NET-app-specific behavior to process core.
- Prefer small changes in `ProcessesService.Runtime.RunStart.cs`.
- Validate with targeted integration tests before broad build proof.
