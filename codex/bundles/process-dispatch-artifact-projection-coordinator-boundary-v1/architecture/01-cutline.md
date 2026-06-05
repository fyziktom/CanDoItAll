# Architecture Cutline

## In Scope

Move source-specific projection orchestration from `ArtifactProjection.cs` into module-local helper/coordinator classes:

- execution artifact projection
- process mock projection
- workspace-written projection
- existing managed artifact projection
- response-text projection
- provider-native browser projection
- completed-decision record-only projection

## Out Of Scope

- Process Core project creation.
- Public contracts for projection.
- Production driver APIs.
- Driver registry.
- UI changes.
- Mobile/tablet/small/medium proof.

## Layering Rule

Use these names consistently:

- `*Facts` / `*Snapshot`: immutable data about a source.
- `*Planner` / `*SourceAdapter`: side-effect-free planning only.
- `*Reader`: explicit file/session/content read helper.
- `*Coordinator`: allowed to perform side effects.
- `*Facade`: orchestration over coordinators.

Planners and adapters must not perform file IO, storage writes, DB writes, service scope creation, or `RecordArtifactAsync`.
