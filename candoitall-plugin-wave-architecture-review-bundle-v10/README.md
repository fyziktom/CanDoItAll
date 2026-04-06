# CanDoItAll plugin-wave architecture review bundle v10

## Purpose
Re-check the current repo after the claimed phase9 closure, prove whether bundle9 is really complete, and give Codex a precise phase10 package that closes the remaining blocker before the next large plugin wave.

## Verdict
**GO with guarded rollout.**

Phase10 is now complete. The remaining blocker from bundle9 is closed:

1. `ProjectStructureAssemblyService.LoadAsync(...)` is zero-write again.
2. stale system-managed projection cleanup moved to the explicit `ProjectStructureProjectionMaintenanceService.RepairAsync(...)` seam.
3. the required exact-name zero-write and repair tests now exist and pass.
4. unknown-manifest connector proof now exercises the shared field editor across all six field types.
5. `gate_check_phase10.py` now passes on the current repo and still emits the expected advisories.

## What phase10 must close
- **HG-10-01**: `LoadAsync` and the active structure-read path must be zero-write.
- **HG-10-02**: stale projection cleanup must move to an explicit maintenance / migration / bootstrap seam that is not reachable from reads.
- **HG-10-03**: behavior tests must prove zero-write reads even when stale system-managed rows, stale layout rows, and legacy compatibility payloads are present.
- **HG-10-04**: the new gate script must fail the current repo and pass only after the behavioral fix.
- **HG-10-05**: manifest-driven connector editors need unknown-plugin regression proof across all field types before the next plugin wave starts.

## What this bundle contains
- an evidence-backed execution report,
- precise subbundles for the remaining blocker and the missing proof,
- stronger anti-evasion rules,
- a new `gate_check_phase10.py` that detects the current false-green scenario,
- explicit required test names so Codex cannot “close” phase10 with vague coverage.

## Validation executed
- `dotnet build CanDoItAll.slnx --artifacts-path C:\repositories\CanDoItAll\artifacts\phase10-validation\solution-build -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --artifacts-path C:\repositories\CanDoItAll\artifacts\phase10-validation\unit-test -v minimal` -> `99/99`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --artifacts-path C:\repositories\CanDoItAll\artifacts\phase10-validation\integration-test -v minimal` -> `115/115`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --artifacts-path C:\repositories\CanDoItAll\artifacts\phase10-validation\component-test -v minimal` -> `241/241`
- `python candoitall-plugin-wave-architecture-review-bundle-v10/scripts/gate_check_phase10.py C:\repositories\CanDoItAll` -> pass with advisories
- `python candoitall-plugin-wave-architecture-review-bundle-v10/scripts/validate_bundle.py C:\repositories\CanDoItAll\candoitall-plugin-wave-architecture-review-bundle-v10 --profile initiative --stage completed` -> pass

## Residual advisories
- The historical phase9 gate is still visibly false-green-shaped.
- Legacy marker/reference compatibility fallbacks are still active and should not expand further.
- `CrmHrServices.cs` and `ProjectWorkbenchModels.cs` remain hotspot warnings.
- Validation used isolated `--artifacts-path` outputs because the default `bin/obj` paths were locked by another local process during this run.

## Important scope note
The current repo also still contains read-only compatibility fallbacks from legacy metadata for markers and node references:
- `ProjectStructureAssemblyService.cs:77-82, 390-408`
- `ProjectNodeBindings.cs:391-395`

Those are **not the main phase10 blocker**, but they remain visible as guarded-rollout risk and should not be expanded further.
