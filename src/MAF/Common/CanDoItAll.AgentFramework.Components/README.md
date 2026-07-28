# CanDoItAll.AgentFramework.Components

## Purpose

Razor components for AgentFramework administration, catalog, execution, and runtime inspection surfaces.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.AgentFramework.Voice/CanDoItAll.AgentFramework.Voice.csproj`

Framework references:

- None

Direct package references:

- `Markdig (1.1.2)`
- `Microsoft.AspNetCore.Components.Web (10.0.5)`
- `CanDoItAll.Components.BaseLib (0.1.4)`
- `CanDoItAll.Components.CanvasLib (0.1.4)`

## Architecture Notes

Keep AgentFramework model contracts, persistence, provider-neutral orchestration, and provider/runtime adapters separated. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly.

`AgentExecutionActivityStatus` is the shared feedback component for an exact
`AgentExecutionActivityStreamId`. It reads the typed sequenced stream from the
beginning, maps `AgentExecutionActivityPhase` to UI labels without parsing message
text, exposes a polite `role="status"` region, marks retained-history gaps, and stops
on terminal, evicted, or unknown results.

The component generation-fences operation changes and disposes its old reader so a
late event cannot overwrite a newer operation. Current-profile reader cancellation is
shown separately from ordinary stream unavailability. Display text is normalized and
bounded; producers remain responsible for publishing sanitized messages.

The component does not hydrate canonical run state. Transcript, approvals, receipts,
artifacts, logs, and metrics continue to come from durable workspace projections.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Agent execution activity and runtime snapshots: `docs/architecture/agent-execution-activity-and-runtime-snapshots.md`
