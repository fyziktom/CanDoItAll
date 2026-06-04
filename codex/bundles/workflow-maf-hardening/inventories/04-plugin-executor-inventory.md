# Plugin Executor Inventory

SB01 inventory date: 2026-05-28.

Proof transcripts:

- `bundle://proof/SB01/transcripts/source-scan.txt`
- `bundle://proof/SB01/transcripts/package-scan.txt`

| Path | Responsibility | Current MAF usage level | Risk | Suggested subbundle owner |
| --- | --- | --- | --- | --- |
| `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs` | Plugin descriptor and `PluginWorkflowExecutorDescriptor` model. Current descriptor includes executor ID, name, description, category, settings renderer key, settings schema, shapes, and default policy. | model-only | critical | SB04 |
| `repo://src/CanDoItAll.Plugins.Abstractions/PluginExecutionContracts.cs` | Plugin service registry and `IPluginWorkflowExecutor` contract. Present, but bundled plugin executors currently implement core `IWorkflowExecutor` directly. | model-only | high | SB04 |
| `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs` | Manifest validation for duplicate executor IDs and capability/metadata consistency. | model-only | medium | SB04 |
| `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginPermissionServices.cs` | Plugin capability grant evaluation used by bundled Gmail/Office365 executor availability checks. | adapter | critical | SB04 |
| `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs` | Plugin log service and workflow executor audit observer integration. | adapter | high | SB05 |
| `repo://src/CanDoItAll.Modules.Plugins/Persistence/PluginLogRecord.cs` | Persistence record for plugin logs, including workflow executor ID. | model-only | high | SB05 |
| `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailBundledPlugin.cs` | Bundled plugin descriptor with Gmail workflow executor descriptors. | adapter | high | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs` | Gmail download/mark-processed workflow executors with OAuth and workflow capability availability checks and simulation descriptors. | adapter | high | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs` | Bundled plugin descriptor with Office365 workflow executor descriptors. | adapter | high | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` | Office365 download/mark-processed workflow executors with OAuth and workflow capability availability checks and simulation descriptors. | adapter | high | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Email/EmailWorkflowPayloadResolver.cs` | Shared email payload resolver used by email-like workflow executors. | adapter | medium | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Docker/DockerBundledPlugin.cs` | Docker host-command workflow executor descriptors. | adapter | critical | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Docker/DockerWorkflowExecutor.cs` | Docker command execution workflow executor. | adapter | critical | SB04 |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs` | Executor invocation policy, timeout/retry/cancellation, redaction, audit observer, and payload-size policy. | adapter | critical | SB04, SB05 |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs` | Current deterministic fake executor coverage for executor catalog, retry, timeout, redaction, routing, and payload limits. | test | critical | SB04 |

SB01 plugin findings:

- Bundled Gmail and Office365 executors already avoid live credentials in default proof through availability descriptors and simulation descriptors, but SB04 must verify this with explicit fake-mode tests.
- Docker is the highest-risk plugin surface because it exposes host-command behavior through workflow executors.
- Plugin descriptors do not yet expose explicit approval requirements or permission-policy fields as first-class descriptor data; availability checks and default policies exist but are not a complete governed executor contract.
- Executor invocation already propagates cancellation, timeout, retry, run ID, redacted settings, and audit observer records at the core `IWorkflowExecutor` boundary.
- SB04 should not introduce dynamic `IServiceProvider` resolution inside execution. The existing typed `IWorkflowExecutor` registration pattern is the safer starting point.
