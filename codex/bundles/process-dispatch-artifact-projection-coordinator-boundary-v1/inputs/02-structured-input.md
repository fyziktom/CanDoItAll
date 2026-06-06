# Structured Input

## Objectives

- Continue module-local dispatcher isolation before any Process Core extraction.
- Move source-specific artifact projection orchestration behind focused module-local coordinators.
- Preserve all current artifact projection source families and their order.
- Centralize candidate state updates after projection write outcomes.
- Add documentation-only driver-readiness vocabulary without production driver APIs.

## Hard Constraints

- Do not create `CanDoItAll.Processes.Core`.
- Do not add `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, or process-driver packages.
- Do not change public process contracts unless required by current tests and documented in the gate.
- Do not remove projection paths or alter their order.
- Do not hide file IO, storage writes, DB writes, or `RecordArtifactAsync` behind pure-looking planners.
- Do not touch UI/Razor/CSS/JS/TS files.
- Do not create small, medium, mobile, phone, or tablet proof artifacts.

## Source Family Order

1. execution artifacts
2. process mock artifacts
3. workspace-written artifacts
4. existing managed artifacts
5. response text artifacts
6. provider-native browser artifacts
7. completed decision artifacts

## Validation Expectations

- Focused tests per migrated source family.
- Build/test/source-scan proof at every critical gate.
- Browser validation remains `N/A` unless the no-UI constraint is violated.
- Known unrelated failures are documented separately instead of being hidden.
