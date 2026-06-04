# Normalized Requirements

| ID | Requirement | Owner |
| --- | --- | --- |
| RQ-001 | Preserve previous MAF, Tooling, execution snapshot, artifact projection, artifact validation, and no-core guardrails. | SB01, SB04, SB08, SB12, SB16 |
| RQ-002 | Refresh live inventory of `ToolValidation.cs`, recovery/finalization consumers, source dependencies, line counts, and tests before production movement. | SB02 |
| RQ-003 | Design a module-local tool-validation snapshot boundary that avoids dispatcher nested-type dependency where practical. | SB03 |
| RQ-004 | Add architecture guards and failing-first scans before moving production behavior. | SB04 |
| RQ-005 | Extract tool receipt fact snapshots and normalization helpers without behavior change. | SB05 |
| RQ-006 | Extract required-tool rule helpers while preserving metadata-required tools, implicit browser proof, process mock satisfaction, dotnet scaffold substitution, and carried implementation proof. | SB06-SB07 |
| RQ-007 | Extract critical tool failure rule helpers while preserving latest-receipt grouping, superseded failure handling, and stack-inapplicable dotnet suppression. | SB09 |
| RQ-008 | Extract completion blocker summary aggregation while preserving all current blocker categories and declared-outcome interactions. | SB10 |
| RQ-009 | Extract completion status decision helper behind a wrapper, without moving final transitions or persistence. | SB11 |
| RQ-010 | Extract retry/recovery decision facts only where pure and stable; do not move recovery journal persistence or provider mutation. | SB13 |
| RQ-011 | Update driver-readiness map for tool-validation semantics without creating driver APIs. | SB14 |
| RQ-012 | Keep browser validation N/A unless UI changes unexpectedly; never create small/medium/mobile proof artifacts. | All |
| RQ-013 | Run focused unit/integration slices, full build, line-count checks, source scans, and completed bundle validator before final closure. | SB15-SB16 |
