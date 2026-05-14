# Target Solution

## OAuth Connection Auto-Resolution

- Add a `PluginOAuthService` method that resolves a configured connection id or, when blank, selects the most recently updated enabled connected OAuth connection for the plugin id and connection key.
- Use that method in Gmail and Office365 workflow executors before calling `GetAccessTokenAsync`.
- Keep failure explicit: invalid configured ids, missing connections, disabled connections, or missing scopes produce actionable errors.

## Project Structure Preview Simulation

- Add Project Structure workflow start simulation options derived from the selected workflow definition.
- Add selected simulated node ids to `ProjectStructureWorkflowNodeStartInput`.
- Build a `WorkflowPreviewSimulationPlan` in `ProjectStructureWorkflowNodeService.StartCoreAsync` for selected project-structure write nodes.
- Reuse the same output shape as the existing project-structure preview simulation templates so downstream workflow steps receive an `inputPayload` envelope.

## Project Context Compatibility

- Let `ProjectStructureWorkflowExecutor` fall back from missing configured `ProjectIdJsonPath` and `NodeIdJsonPath` values to standard project-structure input fields.
- Preserve invalid JSON path failures.
