# Standard Workflow Executors

| Project | Responsibility |
|---|---|
| [Aggregate](CanDoItAll.AgentFramework.WorkflowExecutors.Standard/README.md) | Registers the complete standard executor set |
| [Control](CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control/README.md) | Planning and control-flow nodes |
| [Documents](CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/README.md) | Document conversion and spreadsheet nodes |
| [Media](CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/README.md) | Image generation, inspection, and analysis |
| [Network](CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network/README.md) | Governed HTTP fetch |
| [Project Structure](CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure/README.md) | Project-structure workflow execution |
| [Transforms](CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms/README.md) | JSON and Markdown transformations |
| [Workspace](CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/README.md) | Workspace files and source ingestion |

The aggregate project is the registration entry point. Individual projects keep optional
dependencies isolated.
