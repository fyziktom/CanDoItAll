# Implementation Prompt

Implement the .NET multi-team process refresh only from this bundle. Do not run the software-delivery process.

Before editing, confirm the current template shape in `Templates/Processes`. Then:

1. Add the required .NET child subprocess templates.
2. Update `software-delivery` so .NET app-type recognition, architecture design/review, implementation, runtime command writeback, and UI screenshot writeback are explicit.
3. Keep product mutation restricted to implementation or repair lanes.
4. Update tests so the typed operation contract, subprocess references, and project-structure writeback targets are verified.
5. Record source assertions and command transcripts in `reviews/01-execution-report.md`.

Stop and repair the bundle if a downstream step needs runtime code changes to satisfy the request.
