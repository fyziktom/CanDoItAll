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

## Office365 Processed Category

- Add an Office365 mark-processed workflow executor beside the existing download executor.
- The executor resolves the message id from workflow JSON, uses the OAuth connection resolver, ensures the processed Outlook master category exists, and patches message categories through Microsoft Graph.
- Update the Office365 OAuth descriptor to request `Mail.ReadWrite` and `MailboxSettings.ReadWrite` so missing grants are surfaced as reconnect-required instead of failing late inside the workflow.
- Update the default Office365 workflow template to preserve `runContext.office365Processing`, store the summary with `includeInputPayload`, then mark the message processed after storage succeeds.

## Template Settings Type Preservation

- Normalize YAML scalar settings before serializing executor settings JSON so booleans and numbers remain strongly typed.
- Keep enum names and JSON paths as strings.
