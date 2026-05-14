# Structured Input

| Raw note | Normalized issue | Required outcome |
| --- | --- | --- |
| `N001` | Office365 workflow execution failed because the plugin executor settings contained an empty `connectionId`. | Email OAuth workflow executors resolve a blank connection id from the enabled, connected OAuth connection saved in Plugin settings. |
| `N002` | Project Structure workflow start dialog did not expose the Run Preview option to skip project-structure result storage. | Project Structure workflow starts can pass a preview simulation plan that skips project-structure write executors. |
| `N003` | The skip behavior must be generic across workflows, matching the Gmail workflow preview behavior. | Any workflow with project-structure `CreateAsset` or `CreateTaskNodes` write steps can expose and execute skip simulation from the Project Structure start dialog. |
| `N004` | Identify similar cases where skip must be implemented. | Inventory default workflows with project-structure write steps and verify the generic implementation covers them. |
