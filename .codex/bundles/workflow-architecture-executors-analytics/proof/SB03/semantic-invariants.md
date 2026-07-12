# SB03 Semantic Invariants

## Shared-operation executor adapters

- Invariant ID: SB03-STANDARD-ADAPTERS
- Source raw note: add the missing file, MarkItDown, and image workflow nodes while retaining one reasonable implementation of each key function.
- Expected behavior: `document.to-markdown`, `image.inspect`, and `image.analyze` are stable, runnable contributions; executors own only typed settings/input/output/provider selection mapping and delegate conversion, image file behavior, and provider analysis to SB02 operations.
- Disallowed shallow implementation: duplicate MarkItDown, image parsing, provider gateway calls, or runtime-tool classes inside workflow executors; mark a descriptor runnable without a resolvable implementation.
- Failing-first test: all 13 ID/settings/descriptor/type/catalog assertions failed before implementation in bundle://proof/SB03/failing-document-image.txt.
- Passing test: 65 standard capability tests pass in bundle://proof/SB03/passing-standard-executors.txt and the full build passes in bundle://proof/SB03/passing-build.txt.
- Changed source files: repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/DocumentToMarkdownWorkflowExecutor.cs, repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/ImageInspectWorkflowExecutor.cs, and repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media/ImageAnalyzeWorkflowExecutor.cs.
- Production assertions: real contribution DI exposes exactly one implementation per stable ID; document cancellation reaches conversion/output writes; image analysis selects an enabled vision-capable Chat provider and carries provider/model/tokens/cost status in WorkflowNodeExecutionResult.Usage.
- Red-team negative case: missing paths, malformed JSON paths, failed operations, disabled/non-vision providers, byte limits, cancellation, and unknown prices fail or remain explicitly unknown without a fabricated success/free observation.
- Downstream dependency check: SB05 receives non-null executor usage and SB06 receives typed reflected configuration schema without executor-ID UI branches.

## Directory and spreadsheet operation coverage

- Invariant ID: SB03-BOUNDED-FILE-PREVIEW
- Source raw note: expose missing file and spreadsheet executor capability without producing one node per tool function.
- Expected behavior: existing storage.file and spreadsheet nodes gain appended enum operations for exact directory listing and bounded workbook preview; both delegate the existing shared services.
- Disallowed shallow implementation: reorder persisted enum values, implement recursive listing for ListDirectory, open XLSX independently in the executor, or add redundant node families.
- Failing-first test: missing ListDirectory, Preview, and MaxWorksheets members are recorded in bundle://proof/SB03/failing-storage-spreadsheet.txt.
- Passing test: 25 operation/adapter regression tests pass in bundle://proof/SB03/passing-storage-spreadsheet.txt.
- Changed source files: repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace/WorkspaceFileWorkflowExecutor.cs, repo://src/MAF/WorkflowExecutors/Standard/CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents/SpreadsheetWorkflowExecutor.cs, and repo://src/MAF/Tools/CanDoItAll.Tools.Documents/Spreadsheets/ClosedXmlSpreadsheetDocumentService.cs.
- Production assertions: prior storage values 0..15 and spreadsheet values 0..6 are unchanged; ListDirectory=16 and Preview=7; preview resolves once, opens once, and reports worksheet/row/column truncation.
- Red-team negative case: traversal, missing/malformed workbooks, invalid bounds, service failures, exclusions, and truncation are explicit and tested.
- Downstream dependency check: SB06 can render both new enum choices and MaxWorksheets from configuration metadata automatically.

## Command safety gate

- Invariant ID: SB03-COMMAND-SAFETY
- Source raw note: add missing command capability only when it is safe, typed, approval-aware, cancellable, and masked.
- Expected behavior: command.process remains planned/non-runnable until typed allow-listed recipes propagate cancellation, require approval, strip credentials from child environments, and mask output/failures; the availability message names every blocker.
- Disallowed shallow implementation: raw PowerShell/Python/shell text, a generic command string, inherited application credentials, silent approval bypass, or merely checking cancellation around a non-cancellable call.
- Failing-first test: the generic roadmap message failed the actionable blocker test in bundle://proof/SB03/failing-command-safety-blocker.txt.
- Passing test: the explicit unavailable safety contract passes in bundle://proof/SB03/passing-command-safety-blocker.txt.
- Changed source files: repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/BuiltInWorkflowExecutorDescriptors.cs and repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core/WorkflowExecutorDescriptorFactory.cs.
- Production assertions: the catalog lists the descriptor for planning but WorkflowDefinitionValidator/WorkflowExecutorInvoker reject it as non-runnable.
- Red-team negative case: the source audit finds no raw process/shell construction in WorkflowExecutors; see bundle://proof/SB03/anti-stub.txt.
- Downstream dependency check: SB06 can show the actionable planned state without presenting an executable settings surface.

## Plugin contribution parity and testability

- Invariant ID: SB03-PLUGIN-PARITY
- Source raw note: plugins add workflow executors and must participate in the improved architecture and extensible UI schema.
- Expected behavior: bundled and runtime-package manifest metadata preserves default settings and simulation; runtime activation rejects full descriptor drift; Gmail, Office365, and Docker executors depend on narrow OAuth/API/grant/host ports and execute directly under deterministic tests.
- Disallowed shallow implementation: ID-only package parity, reflection-created payloads instead of ExecuteAsync tests, sealed infrastructure dependencies in adapters, silent malformed JSON replacement, or manifest defaults that differ from the runtime contribution.
- Failing-first evidence: the structural inability to fake sealed dependencies and the missing manifest fields are documented in bundle://proof/SB03/failing-plugin-testability.txt.
- Passing test: 27 unit tests and 19 email-client integration tests pass in bundle://proof/SB03/passing-plugin-executors.txt and bundle://proof/SB03/passing-plugin-integration.txt.
- Changed source files: repo://src/plugins/Abstractions/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs, repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins/PluginWorkflowExecutorRuntimeRegistration.cs, repo://src/plugins/Implementations/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs, repo://src/plugins/Implementations/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs, and repo://src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerWorkflowExecutors.cs.
- Production assertions: real DI maps narrow ports to the existing concrete services; all nine bundled contribution descriptors equal their manifest defaults/schema/simulation/policy; old omitted manifest fields deserialize to safe defaults.
- Red-team negative case: permission denial, invalid email inputs, unsafe Docker arguments, host failure, cancellation, malformed JSON, ID drift, and metadata drift fail before unintended external effects.
- Downstream dependency check: SB06 can render trusted plugin settings from authoritative manifest/contribution metadata and does not need plugin-specific executor branches.
