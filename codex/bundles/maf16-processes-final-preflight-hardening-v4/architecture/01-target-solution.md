# Target Solution

## Architecture Boundary

Keep changes inside the existing CanDoItAll boundaries:

- Agent Framework/MAF adapter: MAF package adoption, sessions, context, tool loop, handoff, workflow, telemetry, and policy proof.
- Process runtime/application services: finalizer validation, artifact validation diagnostics, record-artifact identity scope, recovery, and smoke harness.
- Read models/API/UI: operator-visible artifact obligation status, diagnostics, record id/path, suggested action, and failure ownership.
- Tests/proof: targeted unit, integration, component, and browser/host proof as required by each subbundle.

## Target Behavior

- Every finalizer validation status has an explicit read-model/operator mapping.
- Invalid recorded artifacts remain attached to their artifact record but are displayed as invalid, never satisfied.
- API/UI surfaces preserve compact status language and expose raw diagnostic detail for recovery.
- Full real UI testing remains gated behind the controlled step0 smoke report.
