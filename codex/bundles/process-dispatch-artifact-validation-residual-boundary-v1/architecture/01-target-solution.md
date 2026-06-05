# Architecture Plan

## Boundary Direction

Keep all extracted helpers internal to `CanDoItAll.Modules.Processes`.

Preferred helper naming:

- `ProcessCriticalToolFailureSuppressionRules`
- `ProcessProviderNativeBrowserOutputFacts`
- `ProcessProviderNativeBrowserProbeFailureRules`
- `ProcessArtifactKindClassificationRules`
- `ProcessStorageContentKindRules`
- `ProcessExecutionArtifactMetadataRules`
- `ProcessTechnicalAgentBindingDiagnostics`

## Side-Effect Rules

Pure helper:
- no DB context
- no storage service
- no service scope
- no file writes
- no network/tool calls

Read-only helper:
- may inspect `File.Exists`/`FileInfo.Length` only if explicitly named as browser output probe or file probe
- must not write files or mutate state

Coordinator:
- use only if side effects are unavoidable
- name must include `Coordinator`
- must be covered by source assertions

## No Core / No Driver

Do not create:
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.DriverPacks.*`
- `IProcessDriverPack`
- `ProcessDriverRegistry`
- `ProcessDriverDescriptor`
- any production `DriverPack` source type

## Current Reason Not To Extract Process Core

The module-local seams still reference private dispatcher types and AgentFramework model types. Extracting Core now would either leak those types into public contracts or force a large risky rewrite. Continue with module-local boundaries first.
