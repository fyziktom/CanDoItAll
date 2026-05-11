# Follow-up Request: Workflow Canvas UX, PostgreSQL Test Instance, and Real Scenarios

Source: user message in the active Codex thread after the first executor architecture slice.

## Raw Note

The work is on a good path, but it needs improvements around the workflows canvas.

- The toolbox and selection must be floating windows inside the workflow canvas.
- Adding something new must open a modal.
- Double-clicking a node must open a modal with details and possible edit.
- The workflows page should be split into tabs, for example live dashboard, listing of processes, editor, templates, history, analytics, and related views.
- Create a new PostgreSQL database for this test and put the 20 real-world workflow examples into the testing instance.
- Add projects with project structures so complex cases can test reading/writing project structure, file operations, and asset-node creation.
- If anything is found to be working incorrectly, repair or improve it.
- Ensure workflow APIs are up to date for controlling workflows, similar to processes, so tests can act as a human observer.
- Use the `candoitall-bundle-workflow` skill for this complex task.

## Scope Signal

This follow-up reopens the existing executor bundle. The previous implementation covered executor contracts and first UI exposure, but its browser proof and scenario proof are not strong enough for this stricter canvas and seeded-test-instance requirement.
