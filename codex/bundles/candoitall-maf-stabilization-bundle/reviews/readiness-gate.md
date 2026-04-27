# Readiness Gate

Codex must not mark the work complete unless the following checks are satisfied or explicitly documented as blocked by environment.

## Architecture gates

- [x] Structured-output contracts are preserved across approval/background continuations.
- [x] Machine-critical structured output is validated before success.
- [x] Invalid output cannot be persisted as successful workflow state.
- [x] Bounded repair/retry exists where configured.
- [x] Function invocation middleware enforces tool policy before execution.
- [x] Disabled built-in tools are not attached.
- [x] Finalizer tools are exact-once where required.
- [x] Provider capabilities are centrally resolved and enforced.
- [x] Sessions are not process-state source of truth.
- [x] Generic runtime contains no calculator-specific instructions.
- [x] Observability captures validation, repair, tool policy, finalizer status, raw hash, and final outcome.

## Test gates

- [x] Solution build attempted.
- [x] Unit tests attempted.
- [x] Relevant integration tests attempted.
- [x] Process/calculator regression attempted.
- [x] Environment limitations documented.

## Documentation gates

- [x] Agent output contract docs updated.
- [x] MAF runtime stabilization docs added or updated.
- [x] Provider capability and tool-policy docs added or updated.
- [x] New-agent checklist added.

## Notes

- Full-solution `dotnet test CanDoItAll.slnx --no-build` was not run; the validation used focused unit, integration, process dispatch, process mock, MAF runtime, and guarded live-agent suites plus a full solution build.
- Build/test warning profile is pre-existing: NuGet advisory warnings for `Microsoft.AspNetCore.DataProtection` 10.0.6 and `OpenTelemetry.Api` 1.13.1, `NU1510` pruning hints in the dotnet-watch MCP project, existing xUnit analyzer warnings, existing nullable warnings, and existing component test `ASP0006` warnings.
