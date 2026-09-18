# CanDoItAll.Modules.Processes

Provides process definition, launch, assignment, observation, recovery, project
integration, agent-chat context, and Blazor application surfaces.

The module orchestrates the Processes domain through application/runtime contracts. It
does not write persisted process state outside the process runtime.

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
