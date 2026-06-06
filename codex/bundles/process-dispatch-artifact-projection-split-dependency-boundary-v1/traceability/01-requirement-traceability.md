# Requirement Traceability

## Input Coverage
# Input Coverage Matrix

| Raw note | Requirement IDs | Owning subbundles | Proof method |
| --- | --- | --- | --- |
| Continue smaller dispatcher isolation | RQ-001,RQ-002,RQ-003,RQ-004 | SB01-SB56 | source split, focused tests, line counts |
| Do not rush Process Core | RQ-005 | SB04,SB08,SB12,SB18,SB24,SB30,SB38,SB44,SB50,SB56,SB60,SB64 | no-core scans |
| Preserve all original functionality | RQ-001,RQ-004,RQ-008 | All production movement subbundles | focused unit/integration tests |
| Plan more phases / longer Codex work | RQ-010 | SB01-SB64 | 64 subbundle gate table |
| Prepare for drivers safely | RQ-009,RQ-005 | SB57-SB60 | documentation-only map and no-driver scan |
| No small/medium/mobile proof | RQ-006 | All | N/A browser analytics and proof-path scan |


## Requirement To Subbundle

# Requirement To Subbundle Map

| Requirement | Primary subbundles |
| --- | --- |
| RQ-001: Preserve all existing artifact projection behavior and projection source-family order. | SB04, SB12, SB18, SB24, SB30, SB38, SB44, SB50, SB56, SB64 |
| RQ-002: Split nested artifact projection coordinators into top-level module-local internal classes. | SB13-SB44, SB48 |
| RQ-003: Introduce explicit module-local projection context/host/services boundaries instead of passing the dispatch service into coordinators. | SB05-SB12, SB45-SB50 |
| RQ-004: Keep file-system, storage, record-only and candidate-state side effects explicit and testable. | SB06, SB21, SB28, SB35, SB40-SB44 |
| RQ-005: Do not create Process Core, production process-driver APIs, driver registries, driver packages or public projection contracts. | SB04, SB08, SB12, SB18, SB24, SB30, SB38, SB44, SB50, SB56, SB60, SB64 |
| RQ-006: Do not touch UI/Razor/CSS/JS/TS files and do not create small/medium/mobile proof artifacts. | All subbundles via browser validation logging |
| RQ-007: Reduce `ArtifactProjection.cs` to an orchestration/compatibility facade and remove/deprecate the nested coordinator partial. | SB51-SB56 |
| RQ-008: Keep projection source-family tests and add source scans proving the dependency narrowing. | SB04, SB12, SB18, SB24, SB30, SB38, SB44, SB50, SB56, SB64 |
| RQ-009: Update documentation-only future driver-readiness mapping without production API changes. | SB57-SB60 |
| RQ-010: Use long phased execution with critical refactor gates after several subbundles. | SB01-SB64 |