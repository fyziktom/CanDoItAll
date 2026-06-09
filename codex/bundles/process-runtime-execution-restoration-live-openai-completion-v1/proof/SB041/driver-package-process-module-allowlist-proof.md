# SB041 Driver Package Process Module Allowlist Proof

## Status
Completed.

## Objective
Prove process-module driver package usage stays on an explicit read-only allowlist and does not introduce runtime driver registration or execution-capable host behavior.

## Source-Backed Guard
Focused test:
- `Process_runtime_evidence_readonly_adapter_SB030_INV_003_keeps_driver_references_allowlisted_and_unregistered`

Guard assertions:
- `CanDoItAll.Modules.Processes.csproj` may reference only the approved process driver packages needed for read-only verification.
- The actual files under `src/CanDoItAll.Modules.Processes/Automation/Dispatch` that import `CanDoItAll.Processes.Drivers.` must exactly equal the explicit allowlist.
- Allowed source must not contain direct alpha verifier construction.
- Allowed source must not contain `IServiceCollection`, `AddScoped`, `AddSingleton`, `IProcessDriverRegistry`, `ProcessDriverRuntimeSelector`, `ProcessDriverManagerCommand`, or `ProcessDriverHost`.
- Gateway usage remains through `ProcessDriverVerificationGateway.CreateDefault()` in read-only adapters.

## Validation
- Focused transcript: `bundle://proof/SB042/transcripts/core-domain-boundary-tests.txt`
- TRX: `bundle://proof/SB042/SB042-core-domain-boundary.trx`
- Source assertions: `bundle://proof/SB042/transcripts/source-assertions.txt`

## Closure
SB041 is closed by the passing focused boundary test run and clean runtime-host drift scan.
