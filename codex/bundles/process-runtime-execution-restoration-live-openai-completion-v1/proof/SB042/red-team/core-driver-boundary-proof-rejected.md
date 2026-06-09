# SB042 Red-Team: Core Driver Boundary Proof Rejected

## Rejected Shallow Pass
A shallow Gate N pass could cite the existence of a `Processes.Core` project or driver packages without proving the dependency direction and runtime-host boundary.

## Why It Is Rejected
- Core must not reference modules, infrastructure, drivers, EF, DI, UI, OpenAI, HTTP, Razor, or Blazor dependencies.
- Process module driver package consumption must be limited to explicit read-only adapter/mapper/model files.
- Driver packages must not be auto-registered through DI in the process module.
- Driver usage must not introduce a registry, selector, manager command, runtime host, or execution-capable driver surface.
- Existing driver verification must remain source-evidence read-only.

## Positive Proof Required Instead
- `bundle://proof/SB042/transcripts/core-domain-boundary-tests.txt`
- `bundle://proof/SB042/transcripts/process-core-forbidden-dependency-scan.txt`
- `bundle://proof/SB042/transcripts/source-assertions.txt`
- `bundle://proof/SB042/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB042/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
