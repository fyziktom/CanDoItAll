# Driver Readiness Plan

This bundle prepares future process helper drivers without adding production APIs.

## Allowed

- Documentation-only driver lane maps.
- Test-only sketches under `codex/bundles/...`.
- Permission vocabulary in documentation.
- Evidence manifest vocabulary.
- Read-only verification scenarios.

## Forbidden

- `IProcessDriverPack`
- `IProcessDriverRegistry`
- `ProcessDriverRegistry`
- Driver DI registration
- Runtime dispatch to drivers
- Manager tools that invoke drivers
- Agent tools that expose drivers
- Any mutation-capable helper driver implementation

## Future lanes to keep prepared

- Route decision verification.
- Artifact/evidence/projection verification.
- Runtime proof verification.
- Domain-specific SW-dev helpers (.NET/Rust) in read-only manager mode.
- Office/business-analysis helpers in read-only evidence mode.

## Safety modes to preserve

- Manager-readonly verification.
- Evidence-only explanation.
- Execution-capable mode: explicitly out of scope until a later approval bundle.
