# Normalized Requirements

- **RQ-001**: Preserve all existing artifact projection behavior and projection source-family order.
- **RQ-002**: Split nested artifact projection coordinators into top-level module-local internal classes.
- **RQ-003**: Introduce explicit module-local projection context/host/services boundaries instead of passing the dispatch service into coordinators.
- **RQ-004**: Keep file-system, storage, record-only and candidate-state side effects explicit and testable.
- **RQ-005**: Do not create Process Core, production process-driver APIs, driver registries, driver packages or public projection contracts.
- **RQ-006**: Do not touch UI/Razor/CSS/JS/TS files and do not create small/medium/mobile proof artifacts.
- **RQ-007**: Reduce `ArtifactProjection.cs` to an orchestration/compatibility facade and remove/deprecate the nested coordinator partial.
- **RQ-008**: Keep projection source-family tests and add source scans proving the dependency narrowing.
- **RQ-009**: Update documentation-only future driver-readiness mapping without production API changes.
- **RQ-010**: Use long phased execution with critical refactor gates after several subbundles.
