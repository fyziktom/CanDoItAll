# Executor Inventory

| Executor family | Current implementation | Current project | Target category | Critical behavior | Required failure diagnostics |
| --- | --- | --- | --- | --- | --- |
| Built-in descriptor source | `BuiltInWorkflowExecutorDescriptorSource`, `BuiltInWorkflowExecutorDescriptors` | MAF | WorkflowExecutors.Core plus category factories | Descriptor ids, settings schema, source metadata, deterministic test descriptors. | Duplicate id, missing implementation, invalid schema, unavailable descriptor. |
| Workspace file | `WorkspaceFileWorkflowExecutor` | MAF | Standard.Workspace | Workspace file scope and artifact behavior. | Path escaped/denied, artifact write failed, invalid settings, payload too large. |
| JSON transform | `JsonTransformWorkflowExecutor` | MAF | Standard.Transforms | JSON input/output shape and deterministic transformation. | Invalid JSON, missing path, unsupported transform, payload too large. |
| Markdown render | `MarkdownRenderWorkflowExecutor` | MAF | Standard.Transforms | Generated regex helper, workspace file writes, template replacement. | Missing JSON path, template parse error, output path denied, artifact write failed. |
| Source ingestion | `SourceIngestionWorkflowExecutor` | MAF | Standard.Workspace | File/source discovery, bounds, parse errors; must split path resolution, enumeration, content loading, caps, result shaping, and diagnostics helpers. | File missing, directory missing, extension denied, unauthorized path, max file/content cap, per-file read failure. |
| HTTP fetch | `HttpFetchWorkflowExecutor` | MAF | Standard.Network | Network permission, timeout, response shape. | Timeout, non-success HTTP status, DNS/service unavailable, response too large, download path denied. |
| Delay | `DelayWorkflowExecutor` | MAF | Standard.Control | Timing/cancellation behavior. | Cancellation and invalid delay policy. |
| Human approval | `HumanApprovalWorkflowExecutor` | MAF | Standard.Control | External request creation and approval gate. | Approval denied, expired, missing gate, invalid request payload. |
| Spreadsheet | `SpreadsheetWorkflowExecutor` | MAF | Standard.Documents | Spreadsheet document service, cell write settings. | Workbook missing, invalid range, output write denied, document dependency failure. |
| Project structure | `ProjectStructureWorkflowExecutor` | MAF | Standard.ProjectStructure | Project gateway, task node creation, workflow context; must split settings resolution, JSON path extraction, gateway calls, result shaping, and diagnostics helpers. | Missing project id, missing parent node id, invalid task item, gateway failure, authorization failure. |
| Image generation | `ImageGenerationWorkflowExecutor` | MAF | Standard.Media | Image provider access, workspace paths, artifacts. | Provider unavailable, rate limited, invalid prompt/settings, output artifact failure, timeout. |
| Planned executors | `PlannedWorkflowExecutor` | MAF | Standard.Control | Descriptor-only placeholder behavior. | Unimplemented executor must be unavailable or fail with explicit planned-state diagnostic. |
| Docker | `Docker*WorkflowExecutor` | `src/plugins/CanDoItAll.Plugin.Docker` | WorkflowExecutors.Plugins | Host command grant, approval, output caps, deterministic preview. | Missing grant, approval denied, host command exit code, image pull failure, output cap, redacted command context. |
| Gmail | `GmailDownloadByLabelWorkflowExecutor`, `GmailMarkProcessedWorkflowExecutor` | `src/plugins/CanDoItAll.Plugin.Gmail` | WorkflowExecutors.Plugins | OAuth, secrets, network, external read/write, idempotency receipts, preview. | Missing/expired OAuth, missing secret, provider rate limit, message not found, receipt persistence failure. |
| Office365 | `Office365*WorkflowExecutor` | `src/plugins/CanDoItAll.Plugin.Office365` | WorkflowExecutors.Plugins | OAuth, Microsoft Graph read/write, processed marker receipts, preview. | Missing/expired OAuth, Graph status/rate limit, category/address not found, receipt persistence failure. |
| Runtime package executors | Types assignable to `IWorkflowExecutor` in installed packages | `Modules.Plugins` package loader | WorkflowExecutors.Plugins | Package load context, source/trust metadata, restart requirement. | Package load failure, dependency missing, DI activation failure, executor type name, package id, plugin id, trust/source context. |
| Cognitive Memory | `CognitiveMemoryRecallWorkflowExecutor`, `CognitiveMemoryProbeWorkflowExecutor`, `CognitiveMemoryLearningProposalWorkflowExecutor` | `src/CanDoItAll.Modules.CognitiveMemory` | Feature-module executors consuming WorkflowExecutors.Abstractions | Memory recall/probe/learning proposal descriptors, automation settings, semantic input extraction, and no MAF/Core executor-contract ownership after migration. | Missing automation settings, malformed settings JSON, unavailable semantic dependency, payload too large, cancellation, and redacted memory/source context. |

## Executor Category Tests Required

- Descriptor parity for each moved executor id.
- Descriptor parity for module-provided executor ids such as `cognitive-memory.recall`, `cognitive-memory.probe`, and `cognitive-memory.learning-proposal`.
- Settings schema and default settings parity.
- Permission policy and side-effect descriptor parity.
- Run Preview simulation parity and no production side effects.
- Production executor invocation proof where behavior changes.
- Negative tests for missing grants, missing OAuth/secret, invalid settings, blocked host command, invalid workspace path, and unknown executor id.
- Negative tests for timeout, cancellation, payload too large, artifact failure, provider/service unavailable, rate limit, plugin package load failure, plugin DI activation failure, and malformed template-provided settings.
- No-generic-error assertions: failures must include node id, executor id, source kind, plugin/package/type/tool context where applicable, retryability, redacted technical detail, and repair hint.
