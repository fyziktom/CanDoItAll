# Requirement Traceability

| Requirement | Architecture Coverage | Owning Subbundle | Proof Method |
| --- | --- | --- | --- |
| REQ-001 to REQ-005 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB02 | Core unit and architecture tests |
| REQ-006 to REQ-009 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB06 | Driver stack and strategy factory tests |
| REQ-010 to REQ-014 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB04 | Instance builder tests |
| REQ-015 to REQ-019 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB07 | Artifact lifecycle and recovery tests |
| REQ-020 to REQ-025 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB07 | Manager incident, recovery, and subprocess communication tests |
| REQ-026 to REQ-030 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB08, SB09 | Event, projection, cache, time-range, and browser tests |
| REQ-031 to REQ-037 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB03, SB10 | Template schema, migration, override, and conflict tests |
| REQ-038 to REQ-041 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB03, SB09 | Git wrapper and Git UI tests |
| REQ-042 to REQ-045 | `architecture/01-target-solution.md`, `architecture/02-detailed-design.md` | SB07, SB09 | Branch decision and loop protection tests |
| REQ-046 | `README.md` and `.gitignore` | This architecture task | Git status and ignore behavior |
| REQ-047 to REQ-050 | `plan/01-phase-plan.md` | SB01 through SB10 | Archive manifest, build/test/migration proof |

## Coverage Notes

- The current Process UI/UX anchor is covered by SB09.
- Current reusable pieces are listed in `inventories/01-current-process-surfaces.md`.
- Current-state analysis is covered by `analysis/01-current-state.md`.
- Runtime insufficiency is covered by `analysis/02-runtime-dispatcher-insufficiency.md`.
- Template source-of-truth and projections are covered by SB03 and SB10.
