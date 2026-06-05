# Current State Review

## Previous bundle closure

The subprocess runtime/projection bundle is treated as complete. Its execution report states that SB01-SB24 passed, subprocess lifecycle/projection helpers were added, no Process Core or production driver API was introduced, and `Dispatch.cs` was reduced to roughly 1261 lines.

## Current key production files

| File | Current risk |
| --- | --- |
| `ProcessRunAutomationDispatchService.ImplementationProof.cs` | Mixes generic evidence rules, concrete product mutation/read checks, stack detection, .NET host discovery, runnable proof, receipt ordering, process mock proof, and carried-proof state. |
| `ProcessRunAutomationDispatchService.ToolValidation.cs` | Consumes implementation proof semantics for missing required tools and completion status. |
| `ProcessRunAutomationDispatchService.RecoveryPackets.cs` | Consumes proof/retry facts and must keep recovery directive text compatible. |
| `ProcessRunAutomationDispatchService.Execution.cs` | Carries implementation proof across attempts and historical runs. |
| `ProcessRunAutomationDispatchService.DotnetRunCleanup.cs` | Domain-specific runtime cleanup; do not expand scope unless touched by proof vocabulary. |
| `ProcessRunAutomationDispatchService.WebHostProof.cs` | Dotnet host/web-host proof area; only reference for parity. |
| `ProcessRunAutomationDispatchService.ProjectPaths.cs` | Path aliasing and concrete product path mapping used by implementation proof. |

## Why this is the next seam

The code is now approaching the boundary where future helper drivers will need to express evidence such as build validation, test validation, runnable application proof, document deliverable proof, spreadsheet validation, and business-analysis deliverables. The current implementation-proof partial is where that vocabulary is still encoded as a mixture of tool names, paths, stack keywords, and receipt ordering.

This bundle prepares that seam without introducing actual production drivers.
