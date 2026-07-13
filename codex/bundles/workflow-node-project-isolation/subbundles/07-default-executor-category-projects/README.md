# SB07 - Default Executor Category Projects

## Status

- `Completed`

## Objective

Move default workflow executor implementations out of MAF into logical executor category projects with category-specific registrations, tests, descriptor parity, deterministic preview proof, and side-effect policy coverage.

## Success Criteria

- Default executors are split by logical category instead of one MAF-owned bucket.
- Each category project depends on executor abstractions/core and only the application services it actually needs.
- Built-in executor ids, labels, settings schemas, deterministic test-mode behavior, preview simulation output, side-effect descriptors, and failure behavior are preserved.
- MAF no longer registers concrete default executors directly.
- Large executor classes are split into category helpers by responsibility instead of copied whole into new projects.

## Covered Inputs

- R08, R13, R14, R15, R17, R18.
- Architect note that executors are mixed together and must be split by logical categories.

## Prerequisites

- SB06 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\BuiltInWorkflowExecutorDescriptors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\WorkflowInputPayloadText.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard\StandardWorkflowExecutorServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control\ControlWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control\PlannedWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms\JsonTransformWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms\MarkdownRenderWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace\WorkspaceFileWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace\SourceIngestionWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace\SourceIngestionWorkflowReader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace\SourceIngestionWorkflowPaths.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace\SourceIngestionWorkflowCandidates.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace\SourceIngestionWorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network\HttpFetchWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents\SpreadsheetWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media\ImageGenerationWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure\ProjectStructureWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure\ProjectStructureWorkflowTaskNodes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure\ProjectStructureWorkflowInputResolution.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure\ProjectStructureWorkflowSupport.cs`

## Deliverables

- Category projects:
  - `CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Control`
  - `CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Transforms`
  - `CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace`
  - `CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network`
  - `CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents`
  - `CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Media`
  - `CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure`
- Category registration extensions and descriptor sources.
- Parity tests for each moved default executor.
- Deterministic test-mode and preview tests where existing executors support those behaviors.
- Side-effect, policy-limit, and typed failure-diagnostic tests for file, network, ingestion, spreadsheet, image, and project-structure executors.
- Helper/service splits for `SourceIngestionWorkflowExecutor` and `ProjectStructureWorkflowExecutor` covering settings, path/JSON resolution, gateway/provider calls, caps, result shaping, and diagnostic mapping.

## Dependency Impact

- SB09 cannot harden executor behavior without these projects. SB10 template descriptor validation and SB11 MAF adapter registration depend on category registrations. SB12 UI display must see the same categories and descriptors after the move.

## Validation Depth

- `Critical executor implementation`
- Unit, integration, descriptor parity, deterministic preview, side-effect policy, and service-composition proof.

## Implementation Steps

1. Confirm category ownership for each default executor using `inventories/03-executor-inventory.md`; do not collapse network, documents, and media when dependencies differ.
2. Move one category at a time, keeping registrations and tests green before moving the next category.
3. Preserve executor ids and settings schemas exactly unless a tested compatibility migration is documented.
4. Split large executor classes before or during movement when they mix parsing, IO/provider calls, policy, result shaping, and diagnostics.
5. Replace MAF built-in registration with category registration extensions.
6. Add category-specific tests for valid execution, invalid settings, cancellation, timeout, deterministic preview, side-effect policy, payload caps, artifact failures, and repairable diagnostics.
7. Run focused builds/tests after each category and a combined executor suite at the end.
8. Update workbook rows and proof manifests.

## Scope Exceptions

- Plugin executors are SB08.
- Template loader adoption is SB10.
- UI/API adoption is SB12.

## Do Not Do

- Do not leave default executor implementation in MAF as a fallback.
- Do not merge unrelated executor categories to reduce project count if dependencies differ materially.
- Do not weaken side-effect or approval requirements to simplify tests.
- Do not copy `SourceIngestionWorkflowExecutor` or `ProjectStructureWorkflowExecutor` as a single oversized class into a new project.
- Do not leave external provider/tool failures as generic `InvalidOperationException` messages without typed diagnostic mapping.

## Acceptance Checklist

- [x] Every current default executor has a category project or explicit exception.
- [x] Category registrations replace MAF default registration.
- [x] Descriptor parity tests pass for all moved executors.
- [x] Deterministic preview and side-effect policy tests pass where applicable.
- [x] Per-category negative tests prove typed, repairable diagnostics and redaction.
- [x] Large executors are split by responsibility with focused helper/service tests.
- [x] No concrete default executor remains under `AgentFramework.Maf\Runtime\Workflows` except adapter-only code assigned to SB11.

## Execution Notes

- Added seven standard category projects: Control, Transforms, Workspace, Network, Documents, Media, and ProjectStructure.
- Added a small `CanDoItAll.AgentFramework.WorkflowExecutors.Standard` aggregate registration project that composes category registrations with an explicit executor lifetime.
- Moved built-in descriptor compatibility ownership into `WorkflowExecutors.Core` and replaced the single MAF descriptor source registration with seven category descriptor sources.
- Replaced MAF direct built-in registration with `AddStandardWorkflowExecutors(ServiceLifetime.Singleton)`.
- Replaced `CanDoItAll.Modules.AgentFramework` direct scoped executor registrations with `AddStandardWorkflowExecutors(ServiceLifetime.Scoped)`.
- Moved the shared `WorkflowInputPayloadText` helper into executor core because Workspace and ProjectStructure both consume it.
- Split `SourceIngestionWorkflowExecutor` into reader, path, candidate, and model partial helper files.
- Split `ProjectStructureWorkflowExecutor` into task-node, input-resolution, and support partial helper files.
- Moved ExcelDataReader and PdfPig package ownership from MAF to the Workspace category project.
- Updated workbook Source Map, Executor Categories, Subbundles, and Validation Matrix rows for SB07.

## Validation Notes

- Standard category aggregate build passed with 0 warnings and 0 errors.
- MAF, Hosting, and AgentFramework module consumer builds passed with 0 warnings and 0 errors.
- New `WorkflowExecutorCategoryIsolationTests` passed: `5/5`.
- Existing executor/foundation/hosting/preview regression slice passed: `61/61`.
- Plugin catalog integration passed: `29/29` from an alternate output path because the default Web bin output is locked by an already-running `CanDoItAll.Web` process.
- Static ownership scans found no concrete default executor files or direct default executor registrations under `AgentFramework.Maf\Runtime\Workflows`.
- Anti-stub scan found no placeholder markers in standard executor source or SB07 tests.

## Proof Required

- `proof/SB07/manifest.md` with per-category changed file hashes, build/test transcripts, descriptor parity output, and service registration proof.
- `proof/SB07/semantic-invariants.md` covering executor id stability, descriptor parity, deterministic preview, typed explicit failures, cancellation, timeout, payload caps, redaction, side-effect policy, file responsibility, and no MAF fallback.
- Semantic Adequacy Gate proof including positive execution per category, adversarial invalid settings/side-effect cases, and anti-stub audit.

## Browser Validation Logging

- `N/A`. UI display proof is deferred to SB12.

## Progression Gate

- SB09 cannot start until all planned default executor categories are moved and parity-tested, or exceptions are explicitly documented with downstream owner and risk.

## Suggested Agent Prompt

```text
Implement SB07 only. Move default workflow executors from MAF into the planned category projects one category at a time. Preserve ids, descriptors, deterministic preview, cancellation, diagnostics, and side-effect policy behavior. Split oversized executors by responsibility, add category positive/negative tests and proof. Do not migrate plugins, templates, MAF backend, or UI adoption.
```
