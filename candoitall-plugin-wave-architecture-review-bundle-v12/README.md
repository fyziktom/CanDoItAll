# CanDoItAll plugin-wave architecture review bundle v12

## Purpose
Re-check the newly uploaded repository after the claim that bundle11 was executed, determine whether the current ZIP is actually ready for the pre-plugin runtime wave, and provide a recovery-grade execution bundle when it is not.

## Verdict
**NO-GO. Bundle11 is not closed in the uploaded ZIP.**  
**Phase10 is also regressed again in the uploaded ZIP.**

This is not a small residual gap. The uploaded repository does not merely miss one or two phase11 details:
- the previously closed **phase10 zero-write Workbench read-path** is broken again,
- the previously closed **phase10 unknown-manifest shared editor proof** is broken again,
- the expected **phase11 runtime-plane implementation** is still effectively absent.

## Most important findings
1. `ProjectStructureAssemblyService.LoadAsync(...)` writes during reads again:
   - it calls `RetireLegacyProjectionRowsAsync(...)`,
   - it deletes stale layout rows,
   - it saves changes from the read path.
2. `ProjectStructureProjectionMaintenanceService.cs` is gone, and the repair tests that previously proved zero-write reads are gone too.
3. The unknown-manifest shared editor proof regressed:
   - generic provider/resource field test ids were removed,
   - provider settings no longer pass `Secrets="secrets"` into the shared connector field editor,
   - the shared checkbox branch no longer exposes a `data-testid`.
4. The phase11 runtime-plane implementation requested in bundle11 is still not present:
   - no multi-source automation signal aggregation seam,
   - no canonical trigger registry,
   - no Quartz integration,
   - no durable internal message plane,
   - no hosted workers draining runtime work,
   - no plugin ingress inbox/cursors,
   - no execution telemetry records or optional MQTT bridge.

## Strong evidence that this ZIP is a regression / older snapshot
Compared with the previous user-uploaded repository that had already passed the phase10 gate:
- `src/CanDoItAll.Modules.Workbench/ProjectStructureProjectionMaintenanceService.cs` is missing,
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchProjectionMaintenanceIntegrationTests.cs` is missing,
- `tests/CanDoItAll.Tests.Integration/UnknownConnectorManifestIntegrationTests.cs` is missing,
- `ProjectStructureAssemblyService.cs` has the write-on-read cleanup code back again,
- provider/resource component tests lost the unknown-manifest coverage that existed previously.

See `inventories/06-regression-diff-vs-previous-upload.txt` and the phase10 gate runs for both uploads.

## What bundle12 requires
Bundle12 is a **recovery-first** package:
1. restore the previously closed phase10 guarantees,
2. only then implement the missing phase11 runtime-plane baseline,
3. prove both with code evidence and required tests,
4. finish with green current runs of the phase10, phase11, and phase12 gates.

## Validation note
This review is evidence-backed by repository inspection and gate scripts.  
A full `dotnet test` run was not possible in this container because the .NET SDK was not available.
