# Current State

## OAuth Connection Defaults

- Gmail and Office365 workflow executors deserialize `connectionId` from node settings and throw immediately when it is empty.
- The Plugin settings page creates and stores OAuth-backed `PluginConnectionRecord` plus `PluginOAuthConnectionRecord` rows.
- `PluginOAuthService.GetAccessTokenAsync` only accepts a concrete `PluginConnectionId`, so executor code has no shared way to resolve "use the connected account for this plugin connection key".
- Documentation still tells users to copy connection ids manually into workflow executor settings.

## Project Structure Start Preview

- Main Workflows page and Canvas editor use `WorkflowPreviewInputSupport` to analyze workflow definitions and build `WorkflowPreviewSimulationPlan`.
- `WorkflowPreviewInputSupport` already treats project-structure `CreateAsset` and `CreateTaskNodes` as skippable simulation requirements.
- Project Structure workflow node start uses `ProjectStructureWorkflowNodeService.StartAsync` and currently sends `WorkflowPreviewSimulationPlan.Empty`.
- Project Structure start dialog only confirms the start and shows status; it has no simulation options.
- Project Structure workflow input has `project.id` and `runContext.workflowNodeId`. Some default templates still configure `$.projectId` and `$.nodeId`, so project-structure executors need to fall back to the standard project-structure payload when those top-level aliases are absent.

## Similar Skip Cases

Default workflow templates with project-structure write steps:

- `email-task-creation-router`: `CreateTaskNodes` and `CreateAsset`.
- `gmail-label-email-summary-to-project`: `CreateAsset`.
- `office365-category-email-summary-to-project`: `CreateAsset`.
- `mouser-order-reconciliation`: `CreateAsset`.
- `mouser-purchasing-summary`: `CreateAsset`.
- `seamark-xray-device-folder-summary`: `CreateAsset`.
- `seamark-price-list-extraction`: `CreateAsset`.
- `iotfactory-financial-plan-review`: `CreateAsset`.
- `internet-research-capture`: `CreateAsset`.

The generic fix should cover all current and future workflows with project-structure write executors rather than hard-coding these keys.
