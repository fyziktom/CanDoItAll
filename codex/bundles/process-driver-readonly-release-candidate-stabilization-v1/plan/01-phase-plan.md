# Phase Plan

## Execution Order

Execute subbundles in numeric order. Every third subbundle is a critical gate and must pass before downstream work proceeds.

## Subbundle Dependency Map

```mermaid
gantt
    title Read-only driver release-candidate stabilization
    dateFormat  X
    axisFormat  %s
    section P01 Crash/source proof reconciliation and full-unit baseline
    SB001 Re-read branch, compare report with source : sb001, 1, 1
    SB002 Rerun or record build/full-unit/focused/so : sb002, 2, 1
    SB003 Gate A: source-backed baseline, no report- : sb003, 3, 1
    section P02 Core and package topology governance
    SB004 Refresh Core public API and consumer allow : sb004, 4, 1
    SB005 Refresh driver package topology and soluti : sb005, 5, 1
    SB006 Gate B: Core driver-free and package topol : sb006, 6, 1
    section P03 Process domain adapters split
    SB007 Split artifact/Office/business/aggregation : sb007, 7, 1
    SB008 Move payload/observation records and lane  : sb008, 8, 1
    SB009 Gate C: split adapters preserve behavior a : sb009, 9, 1
    section P04 Payload builder split and shared evidence helpers
    SB010 Split ProcessReadOnlyVerificationPayloadBu : sb010, 10, 1
    SB011 Extract shared identity/scope/evidence-ref : sb011, 11, 1
    SB012 Gate D: payload builder parity and hash/UR : sb012, 12, 1
    section P05 Batch orchestrator hardening
    SB013 Reduce repeated response mapping and add l : sb013, 13, 1
    SB014 Add empty-batch, denied-batch and partial- : sb014, 14, 1
    SB015 Gate E: batch orchestration remains explic : sb015, 15, 1
    section P06 Verification gateway release-candidate hardening
    SB016 Strengthen explicit gateway lane surface a : sb016, 16, 1
    SB017 Add negative tests for generic dispatch, o : sb017, 17, 1
    SB018 Gate F: gateway cannot become runtime host : sb018, 18, 1
    section P07 Shared evidence policy convergence
    SB019 Centralize bounded size, URI, hash and sup : sb019, 19, 1
    SB020 Add cross-lane evidence mismatch and conte : sb020, 20, 1
    SB021 Gate G: evidence policy uniform across all : sb021, 21, 1
    section P08 Audit, redaction and no-mutation convergence
    SB022 Normalize audit lane/evidence references a : sb022, 22, 1
    SB023 Add redaction/leakage corpus for all lanes : sb023, 23, 1
    SB024 Gate H: accepted/denied responses carry no : sb024, 24, 1
    section P09 Manager-visible read-only projection planning
    SB025 Add DTO-only projection planner over batch : sb025, 25, 1
    SB026 Add projection tests for summaries, denied : sb026, 26, 1
    SB027 Gate I: projection planner has no persiste : sb027, 27, 1
    section P10 Observation aggregation release-candidate hardening
    SB028 Harden aggregation against mixed lanes, mi : sb028, 28, 1
    SB029 Add aggregate consistency tests across fiv : sb029, 29, 1
    SB030 Gate J: aggregation remains read-only and  : sb030, 30, 1
    section P11 Multi-domain corpus and fake-proof hardening
    SB031 Expand transcript/runtime/artifact/Office/ : sb031, 31, 1
    SB032 Add fake-proof rejection tests for non-emp : sb032, 32, 1
    SB033 Gate K: corpus exercises production parser : sb033, 33, 1
    section P12 Process module driver consumer allow-list
    SB034 Generate exact process-module driver/Core  : sb034, 34, 1
    SB035 Add tests preventing unlisted driver usage : sb035, 35, 1
    SB036 Gate L: process module driver coupling is  : sb036, 36, 1
    section P13 Contract version and compatibility governance
    SB037 Refresh v1.x contract version history and  : sb037, 37, 1
    SB038 Add API snapshot tests for gateway, abstra : sb038, 38, 1
    SB039 Gate M: version/API governance is source-b : sb039, 39, 1
    section P14 Docs, samples and migration notes
    SB040 Update package README samples to supplied- : sb040, 40, 1
    SB041 Add process-module adapter migration and s : sb041, 41, 1
    SB042 Gate N: docs do not imply runtime host or  : sb042, 42, 1
    section P15 Runtime-host prerequisite backlog
    SB043 Update runtime-host approval matrix with u : sb043, 43, 1
    SB044 Add tests rejecting accidental approval la : sb044, 44, 1
    SB045 Gate O: runtime host remains blocked : sb045, 45, 1
    section P16 Release-candidate smoke matrix
    SB046 Run solution build, full unit, focused uni : sb046, 46, 1
    SB047 Record package/dependency/no-UI/no-secret/ : sb047, 47, 1
    SB048 Gate P: release-candidate smoke passes : sb048, 48, 1
    section P17 Red-team and semantic adequacy audit
    SB049 Audit every critical manifest for producti : sb049, 49, 1
    SB050 Run red-team proof rejecting report-only a : sb050, 50, 1
    SB051 Gate Q: proof quality is artifact-backed : sb051, 51, 1
    section P18 Final validator, handoff and next decision
    SB052 Run prepared/completed validators after ex : sb052, 52, 1
    SB053 Write final architecture decision and next : sb053, 53, 1
    SB054 Gate R: final handoff zip and closure : sb054, 54, 1
```

## Critical Subbundles

- SB003: Gate A: source-backed baseline, no report-only closure
- SB006: Gate B: Core driver-free and package topology guarded
- SB009: Gate C: split adapters preserve behavior and no-side-effect scans
- SB012: Gate D: payload builder parity and hash/URI behavior preserved
- SB015: Gate E: batch orchestration remains explicit and read-only
- SB018: Gate F: gateway cannot become runtime host
- SB021: Gate G: evidence policy uniform across all lanes
- SB024: Gate H: accepted/denied responses carry no-mutation and redacted audit facts
- SB027: Gate I: projection planner has no persistence/UI/manager command
- SB030: Gate J: aggregation remains read-only and immutable
- SB033: Gate K: corpus exercises production parsers/verifiers
- SB036: Gate L: process module driver coupling is explicit and bounded
- SB039: Gate M: version/API governance is source-backed
- SB042: Gate N: docs do not imply runtime host or side-effect approval
- SB045: Gate O: runtime host remains blocked
- SB048: Gate P: release-candidate smoke passes
- SB051: Gate Q: proof quality is artifact-backed
- SB054: Gate R: final handoff zip and closure

## Phase Gates`r`n`r`n- Each critical gate requires build/focused tests/source scans and artifact-backed manifests. Gate P/Q/R require full smoke, red-team, prepared and completed validators.
