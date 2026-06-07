# Assumptions And Risks

## Assumptions
- The latest branch contains the completed `process-driver-contract-api-verification-alpha-boundary-v1` bundle and contract-only driver abstractions.
- The first production alpha can be a pure library/service package that is not wired into runtime dispatch.
- Verification-only alpha will parse provided transcript text, not execute commands or read arbitrary paths.

## Critical Path Risks
- A driver "alpha" might accidentally become runtime infrastructure by adding registry/selector/DI/manager commands.
- A transcript verifier might try to run `dotnet`, `cargo`, or read files directly instead of consuming provided evidence content.
- Audit/redaction may be treated as optional.
- Core could start referencing driver abstractions, reversing dependency direction.
- A domain lane might silently authorize side effects in docs or tests.
- Over-broad source scans could miss a narrow forbidden API hidden in production source.

## Validation Risks
- Build-only proof cannot detect unsafe runtime surface.
- Unit tests must include negative cases for every denied operation.
- Focused tests must cover .NET warnings/errors, test failures, missing artifacts, unsupported target frameworks, Rust cargo failures, and safe no-issue transcript cases.
- Anti-stub scan must cover production driver alpha source, not only dispatch files.

## Reopen Triggers
- Any production interface named registry, selector, host, provider, runtime, manager command, DI extension, or executor is introduced.
- Any alpha code starts a process, reads arbitrary files, writes storage/workspace, mutates process state, or schedules retry.
- Driver abstractions reference module/infrastructure/AgentFramework/UI packages.
- Core references driver abstractions.
- Verification response can omit audit/redaction/no-mutation proof.
