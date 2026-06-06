# Source scan template

Required scans:

```powershell
rg -n "CanDoItAll\.Processes\.Core|IProcessDriverPack|IProcessDriverRegistry|ProcessDriverRegistry|IProcessHelperDriver" src
rg -n "TODO|NotImplementedException|throw new NotImplementedException|return default|fixture-specific|stub" src/CanDoItAll.Modules.Processes/Automation/Dispatch
rg -n "IProcessArtifactProjectionHost" src/CanDoItAll.Modules.Processes/Automation/Dispatch/*ProjectionCoordinator*.cs
```

The final scan should fail if source-family coordinators still depend on the broad host after the migration gates.
