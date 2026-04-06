# CanDoItAll plugin-wave architecture review bundle v10

## Purpose
Re-check the current repo after the claimed phase9 closure, prove whether bundle9 is really complete, and give Codex a precise phase10 package that closes the remaining blocker before the next large plugin wave.

## Verdict
**NO-GO until phase10 closes.**

Bundle9 is **not fully complete**. The critical miss is that the structure read path is still not read-only:

1. `ProjectStructureAssemblyService.LoadAsync(...)` still calls `RetireLegacyProjectionRowsAsync(...)` from the hot read path (`src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:135`).
2. `LoadAsync(...)` still deletes stale projection layout rows and persists the delete during reads (`src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:167-175`).
3. `RetireLegacyProjectionRowsAsync(...)` still removes stale system-managed nodes/links and saves changes (`src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:361-388`).
4. The phase9 gate script produced another false green because it only looked for the old normalization method names and never failed on direct/transitive write operations inside the load path (`candoitall-plugin-wave-architecture-review-bundle-v9/scripts/gate_check_phase9.py`).

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

## Important scope note
The current repo also still contains read-only compatibility fallbacks from legacy metadata for markers and node references:
- `ProjectStructureAssemblyService.cs:77-82, 390-408`
- `ProjectNodeBindings.cs:391-395`

Those are **not the main phase10 blocker**, but they remain visible as guarded-rollout risk and should not be expanded further.
