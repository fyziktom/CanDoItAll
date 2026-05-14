# Normalized Requirements

| Requirement | Source notes | Observable success criteria |
| --- | --- | --- |
| `R001` OAuth email executors resolve blank connection ids automatically. | `N001` | Gmail and Office365 executor runs with blank `connectionId` use the connected OAuth connection saved in plugin settings and write the resolved connection id into their workflow processing payload. |
| `R002` OAuth connection selection is explicit and safe. | `N001` | Invalid non-empty connection ids fail; disconnected or reconnect-required OAuth records are not selected. |
| `R003` Project Structure workflow start supports preview skip simulation. | `N002`, `N003` | The start dialog exposes selectable skip options for project-structure write nodes and passes selected node ids to workflow runtime as a `WorkflowPreviewSimulationPlan`. |
| `R004` Skip simulation is generic. | `N003`, `N004` | Detection is based on executor id `project-structure` and operations `CreateAsset`/`CreateTaskNodes`, not workflow template keys. |
| `R005` Existing project-structure workflow payloads remain compatible. | `N002`, `N003` | Project-structure write executors can resolve project and parent node from standard Project Structure workflow input when top-level `projectId` or `nodeId` aliases are absent. |
| `R006` Similar cases are identified. | `N004` | Bundle analysis lists all current default workflows with project-structure writes and the generic implementation is validated against at least one non-Office workflow shape. |
| `R007` Office365 processed category mutation is part of the default workflow. | `N005` | The default Office365 email summary workflow marks the processed message by removing `CanDoItAllSummaryTest` and adding `CanDoItAllSummaryTestProcessed` after the summary asset is stored. |
| `R008` Office365 processed category is created when absent. | `N005` | The Office365 plugin checks Outlook master categories and creates the processed category before patching message categories. |
| `R009` Workflow-template settings preserve scalar types. | `N006` | Boolean and numeric settings loaded from workflow YAML remain booleans/numbers in executor settings JSON so preview analyzers and executors can deserialize them. |
