# CanDoItAll.Modules.Processes

Provides process definition, launch, assignment, observation, recovery, project
integration, agent-chat context, and Blazor application surfaces.

The module orchestrates the Processes domain through application/runtime contracts. It
does not write persisted process state outside the process runtime.

Live Processes shows the selected run even outside the history window. Launch feedback
is only pending while that run's projection is unavailable; the projection owns its
actual status. Structure launch dialogs retain browser intent identities across uncertain
acknowledgements, and retire them after continuation and link delivery are confirmed.
Opening a retained accepted launch observes that same run; it does not rework a blocked
step. Preparing another launch creates a new intent.

The browser lifecycle gate correlates the latest required host with its own successful
stop receipt in the current execution. It accepts multiple hosts stopping in either
order and requires browser evidence from within the matching host's lifetime. A later
stop for another host cannot replace or invalidate that cleanup proof.

```powershell
dotnet build .\src\Modules\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj
```

Mapped Workflow dispatch passes the actual Process claim to a typed owner admission
port. The saved Process assignment, sealed outcome contract and original launch source
are rechecked through the Workflow run/Started commit. Workflow identity comes from
the typed assignment binding; an executor display identifier is not an Agent identity.
Recovery reads at most two children for the exact Process run and step instance and
refuses ambiguity. Reclaim and step rework retain the existing one-child-per-instance
semantics; a new step instance is a new mapped effect. Artifact and tool-receipt mapping
remain unsupported. A lost acknowledgement preserves the original child identity.
