# Normalized Requirements

- **RQ-001**: Review the current maf-processes-refactor branch and confirm the previous pre-execution/materialization bundle closure before planning the next bundle. Owner: SB01, SB24.
- **RQ-002**: Do not start Process Core extraction in this bundle. Owner: SB01, SB04, SB20, SB24.
- **RQ-003**: Do not introduce production process driver APIs, driver registries, or driver-pack projects. Owner: SB01, SB04, SB20, SB24.
- **RQ-004**: Continue decomposing large dispatch services through module-local seams and abstractions. Owner: SB02-SB19.
- **RQ-005**: Preserve original subprocess dispatch behavior, including start transition, capability-gap handling, terminal status mirroring, finalizer invocation, and artifact projection. Owner: SB05-SB19, SB22.
- **RQ-006**: Preserve subprocess artifact projection semantics: source artifact selection, gap diagnostics, markdown content, storage path, lineage, external reference key, journal entry, and SaveChanges timing. Owner: SB11-SB17, SB22.
- **RQ-007**: Keep side effects explicit and outside pure helpers: EF writes, file writes, subprocess run creation/observation, transitions, and finalizer calls. Owner: SB03, SB07, SB14, SB15, SB18.
- **RQ-008**: Add refactor gates every few subbundles with focused tests, source scans, line counts, and red-team checks. Owner: SB04, SB08, SB16, SB19, SB23, SB24.
- **RQ-009**: Prepare driver-readiness vocabulary as documentation only, especially delegated process/subprocess evidence categories. Owner: SB20.
- **RQ-010**: Do not waste time on small/medium/mobile UI proof; browser validation is N/A unless UI unexpectedly changes, then large desktop/PC only. Owner: SB01, SB04, SB22, SB24.
- **RQ-011**: Produce enough phased work for Codex to run longer and not finish after a shallow helper extraction. Owner: SB01-SB24.
