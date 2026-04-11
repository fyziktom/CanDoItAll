# Assumptions and limitations

## Assumptions
- The current uploaded repository snapshot is the source of truth for current process-module architecture.
- The bundle may add sidecar metadata and overlay code even where the current runtime still consumes projected import envelopes.
- Template pack JSON and workbook artifacts are treated as authored source; import envelopes are treated as projections.

## Limitations
- `dotnet` SDK was not available in this container.
- The corrective canvas-chrome de-hardcode work is packaged as a dedicated subbundle because the current module still contains hardcoded authoring chrome in `ProcessCanvasSurfaceFactory`.
- The bundle focuses on execution-grade preparation and explicit architectural control; it does not claim that every optional UI enhancement has already been merged into the repository.
