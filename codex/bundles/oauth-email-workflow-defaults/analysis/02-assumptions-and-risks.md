# Assumptions And Risks

## Assumptions

- Blank `connectionId` means "use the most recently updated enabled connected OAuth connection for this plugin connection key".
- A non-blank invalid `connectionId` should still fail predictably rather than silently selecting another account.
- Project-structure write skipping is preview-only. Normal starts without selected simulation should still write results.
- Office365 processed marking runs after summary storage succeeds so a failed storage step does not move the message out of the source category.

## Critical Path Risks

- OAuth auto-resolution must not select a disconnected or reconnect-required OAuth connection.
- Project Structure start must pass the simulation plan into the same workflow runtime path used by the Workflows and Canvas preview surfaces.
- Project-structure executor path fallback must not hide malformed JSON path settings; invalid paths should still fail.
- Office365 category mutation requires broader Graph scopes; old connections must surface reconnect-required instead of failing late with Graph authorization errors.
- Workflow template scalar repair must not convert enum names or JSON paths into non-string values.

## Validation Risks

- Existing running web processes can lock build outputs. Use targeted test output artifacts or stop the process before full builds.
- UI proof must check the Project Structure start dialog, not only the Workflows page preview dialog.

## Reopen Triggers

- Any email plugin executor still throws only because `connectionId` is blank.
- Project Structure start dialog does not list project-structure write steps for a workflow that contains `CreateAsset` or `CreateTaskNodes`.
- A simulated project-structure write is still executed as a real project mutation.
- Existing workflow templates fail to resolve project context from `$.project.id` or parent workflow node context.
- Office365 workflow completes without moving the processed message from `CanDoItAllSummaryTest` to `CanDoItAllSummaryTestProcessed`.
- The actual seeded Office365 workflow still does not expose the project-structure storage skip option in Run Preview.
