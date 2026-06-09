# SB042 Semantic Invariants

## Status
Completed.

## Invariant SB040_INV_001
- Invariant ID: `SB040_INV_001`
- Source raw note: Process Core must remain generic and not become a module/runtime dumping ground.
- Expected behavior: `CanDoItAll.Processes.Core` references only `CanDoItAll.Processes.Contracts` and has no module, infrastructure, driver, EF, DI, UI, OpenAI, HTTP, Razor, or Blazor dependencies.
- Disallowed shallow implementation: moving runtime/module dependencies into Core or relying on broad contract names as evidence of a boundary violation.
- Passing proof: `bundle://proof/SB042/transcripts/process-core-forbidden-dependency-scan.txt`

## Invariant SB041_INV_001
- Invariant ID: `SB041_INV_001`
- Source raw note: driver packages may be consumed only through audited process module read-only verification surfaces.
- Expected behavior: the exact driver-consuming file set equals the explicit allowlist, approved driver package references exist, and allowed source contains no DI registration, registry, selector, manager command, or driver host.
- Disallowed shallow implementation: broad process-module driver imports, direct alpha verifier construction, auto-registration, or runtime execution hooks.
- Passing test: `Process_runtime_evidence_readonly_adapter_SB030_INV_003_keeps_driver_references_allowlisted_and_unregistered`

## Invariant SB042_INV_001
- Invariant ID: `SB042_INV_001`
- Source raw note: Gate N must prove Core/domain boundary without introducing runtime driver hosts.
- Expected behavior: focused boundary tests pass, Core forbidden-dependency scan is clean, active bundle-path scan is clean, and forbidden runtime-host scan is clean.
- Disallowed shallow implementation: report-only boundary assertion or runtime driver host/selector/registry proof.
- Failing-first/negative proof: `bundle://proof/SB042/red-team/core-driver-boundary-proof-rejected.md`
- Passing test: `bundle://proof/SB042/transcripts/core-domain-boundary-tests.txt`
- Source assertions: `bundle://proof/SB042/transcripts/source-assertions.txt`

## Shallow-Pass Trap
A fake Gate N closure could prove only that Core builds. SB042 rejects that by requiring clean forbidden-dependency scans, exact process-module driver allowlist testing, no runtime-host surface, and no active bundle paths in source/tests.

## Semantic Positive Proof
- `bundle://proof/SB040/process-core-genericity-scan.md`
- `bundle://proof/SB041/driver-package-process-module-allowlist-proof.md`
- `bundle://proof/SB042/transcripts/core-domain-boundary-tests.txt`
- `bundle://proof/SB042/transcripts/source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB042/red-team/core-driver-boundary-proof-rejected.md`

## Anti-Stub Audit
- `bundle://proof/SB042/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB042/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No active bundle paths or forbidden runtime driver host surfaces were found in scoped source/tests.
