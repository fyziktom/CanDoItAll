# SB040 Process Core Genericity Scan

## Status
Completed.

## Objective
Prove `CanDoItAll.Processes.Core` stays generic and does not depend on process module infrastructure, driver packages, UI, EF, DI, OpenAI, HTTP, Razor, or Blazor surfaces.

## Project Boundary
`repo://src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj` contains one project reference:
- `repo://src/CanDoItAll.Processes.Contracts/CanDoItAll.Processes.Contracts.csproj`

## Forbidden Dependency Scan
Command captured in `bundle://proof/SB042/transcripts/process-core-forbidden-dependency-scan.txt`.

Result:
- Exit code: `1`
- Matches: none

## Allowed Contract Usage
The Core project uses shared process contracts such as `ProcessRunStatus`, `ProcessStepRunStatus`, artifact kinds, finalizer descriptors, routing snapshots, and process-core artifact models. Those are domain contracts, not module/runtime dependencies.

## Closure
SB040 is closed by the clean project reference inspection and forbidden-dependency scan.
