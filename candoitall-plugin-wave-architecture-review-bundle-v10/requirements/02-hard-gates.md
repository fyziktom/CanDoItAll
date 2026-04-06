# Hard gates

HG-10-01. `ProjectStructureAssemblyService.LoadAsync(...)` and the active structure-read seam must be zero-write.

HG-10-02. Stale system-managed projection cleanup and orphan layout cleanup must live outside the read path in an explicit repair / maintenance seam.

HG-10-03. The required zero-write and explicit-repair tests must exist and pass.

HG-10-04. The phase10 gate script must fail the current repo shape and pass only after the behavioral fix.

HG-10-05. Unknown provider/resource plugin manifest tests must prove shared-editor coverage across all field types needed for the next plugin wave.

## Manual gates
MG-10-01. Keep a visible inventory of remaining marker/reference compatibility fallbacks so they do not silently expand again.
MG-10-02. Keep hotspot warnings for `CrmHrServices.cs` and `ProjectWorkbenchModels.cs`; they stay advisory in phase10.
