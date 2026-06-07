# Package Namespace Versioning Policy

## Decision
- Production contract types live in `CanDoItAll.Processes.Drivers.Abstractions`.
- The project has no package references and no project references.
- The package is contract-only: immutable records, enums, and value metadata only.
- The initial contract version is `ProcessDriverContractVersion(1, 0, 0)`.

## Namespace Rules
- Permission and denial contracts live under `CanDoItAll.Processes.Drivers.Abstractions.Permissions`.
- Audit and redaction contracts live under `CanDoItAll.Processes.Drivers.Abstractions.Audit`.
- Evidence and transcript reference contracts live under `CanDoItAll.Processes.Drivers.Abstractions.Evidence`.
- Verification request, response, diagnostic, and version contracts live under `CanDoItAll.Processes.Drivers.Abstractions.Verification`.

## Forbidden Package Surfaces
- No runtime selector, registry, host, provider, manager command, DI extension, shell runner, Office connector, storage writer, workspace writer, claim mutator, transition mutator, finalizer applier, retry scheduler, or process mutation API.
- No reference from `CanDoItAll.Processes.Core` to `CanDoItAll.Processes.Drivers.Abstractions`.
- No reference from the driver abstractions project to Modules, Infrastructure, AgentFramework, EF, UI, storage, workspace, or external connector packages.

## Compatibility Rule
- Version increments require architecture tests to prove dependency cleanliness, public contract inventory, no forbidden runtime token drift, and no UI/media drift.
- Any future execution-capable contract must be approved in a separate bundle after sandbox, allowlist, audit persistence, secret masking, lifecycle ownership, and negative tests are complete.
