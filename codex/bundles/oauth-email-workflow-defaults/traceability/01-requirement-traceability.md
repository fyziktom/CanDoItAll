# Requirement Traceability

| Raw note | Requirement | Owning subbundle | Source files | Proof |
| --- | --- | --- | --- | --- |
| `N001` | `R001`, `R002` | `01-oauth-connection-defaults` | `PluginOAuthService.cs`, `GmailWorkflowExecutor.cs`, `Office365WorkflowExecutor.cs` | Targeted plugin integration tests and email executor tests. |
| `N002` | `R003`, `R005` | `02-generic-project-storage-skip-preview` | `ProjectStructureWorkflowNodeService.cs`, `ProjectStructureAgentContracts.cs`, `ProjectStructureCanvasDialogs.razor`, `ProjectStructureWorkflowExecutor.cs` | Component/integration tests and browser proof on Project Structure start dialog. |
| `N003` | `R003`, `R004`, `R005` | `02-generic-project-storage-skip-preview` | Same as `N002`, plus workflow template inventory. | Generic workflow analysis test covering `CreateAsset` and `CreateTaskNodes`. |
| `N004` | `R006` | `02-generic-project-storage-skip-preview` | `Templates/Workflows/workflows/default-workflows.yaml` | Inventory recorded in `analysis/01-current-state.md` and tests proving generic detection. |
| `N005` | `R007`, `R008` | `03-office365-processed-category-and-template-settings` | `Office365GraphClient.cs`, `Office365WorkflowExecutor.cs`, `Office365BundledPlugin.cs`, `Office365PluginConstants.cs`, `default-workflows.yaml` | Office365 client mutation test, OAuth scope integration tests, component OAuth test, build. |
| `N006` | `R003`, `R004`, `R009` | `03-office365-processed-category-and-template-settings` | `WorkflowTemplatePackLoader.cs`, `ProjectStructureWorkflowPreviewSimulationSupport.cs`, `default-workflows.yaml` | Unit test loads the actual Office365 template and verifies `store-office365-summary` is skippable. |
