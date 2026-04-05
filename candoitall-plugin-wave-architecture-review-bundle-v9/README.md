# CanDoItAll plugin-wave architecture review bundle v9

## Purpose
Re-review the codebase after the phase8 refactor and decide whether the architecture is finally safe for the next large plugin wave (email, LinkedIn, custom APIs, and other connector-driven features).

## Verdict
**GO with guarded rollout.**

Phase 9 is now implemented and runtime-validated in a real .NET environment. The blocking findings that kept v9 in `NO-GO` are closed:
1. legacy binding/carrier fields are retired from `ProjectObjectRecord` and `Workbench_ProjectObjects`,
2. binding state is composed through binding facets/read models instead of hydrating legacy node carrier fields,
3. marker truth is persisted only as `MarkersJson`,
4. provider/resource editors render manifest-driven connector fields through a shared config-state editor,
5. custom plugins persist plugin key as the authoritative identity and do not synthesize fake legacy enums,
6. node references are open-world string-keyed rows with typed helpers only at the edge,
7. load paths are read-only and no longer write compatibility normalization during `GetStructureAsync`,
8. a generic durable connector-command boundary exists for future write-side plugins.

Closure proof also caught and fixed one real regression during validation: the manifest-driven connector editor used invalid `ValueExpression` lambdas for Blazor field identifiers. That bug was corrected in the shared editor and revalidated through component and Playwright coverage.

## Why this bundle is stronger than v8
The previous gate structure produced a false green because it only scanned a narrow set of files and symbols. In this revision:
- hard gates scan the **whole relevant repo surface**, not just one file,
- forbidden-pattern rules explicitly fail on **moved partial classes** and **compatibility shims living in active paths**,
- closure requires **code + tests + proof**, not just ADR text,
- plugin readiness now includes **manual gate MG-01** for the future write-side connector boundary.

## Runtime validation status
Completed in Codex on April 5, 2026.

Validation that actually ran:
- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "(FullyQualifiedName~Settings_page_supports_manifest_driven_provider_management|FullyQualifiedName~Resources_page_supports_manifest_driven_connector_selection|FullyQualifiedName~Agents_workspace_supports_creation_and_governance_profile)" -v minimal`
- `python C:\repositories\CanDoItAll\candoitall-plugin-wave-architecture-review-bundle-v9\scripts\gate_check_phase9.py C:\repositories\CanDoItAll`

Observed results:
- unit: `99/99` passed
- integration: `110/110` passed
- components: `239/239` passed
- targeted Playwright: `3/3` passed
- phase9 hard gate: pass, with only non-blocking hotspot warnings

Residual guarded-rollout notes:
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` and `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` still exceed the advisory hotspot thresholds.
- `CanDoItAll.Mcp.DotNetWatch` still emits existing non-blocking `NU1510` package-pruning warnings during solution build.
- `tests/CanDoItAll.Tests.Integration/WorkforceProfileIntegrationTests.cs` still emits the unrelated existing `xUnit2031` analyzer warning.
