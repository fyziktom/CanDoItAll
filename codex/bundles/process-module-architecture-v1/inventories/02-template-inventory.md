# Template Inventory

## Current Template Pack

Current source: `repo://Templates/Processes/manifest.json`

Observed pack identity:

- Pack key: `candoitall-process-template-pack`
- Version: `2.1.0-live-run-governance`
- Source format: file-based JSON with sidecar Markdown, Mermaid, and current-module projections
- Process count in manifest: 24

Representative process families:

- .NET solution setup and development slices
- Blazor application delivery and repair
- Software delivery and release governance
- Branching code review
- Business plan development
- Customer onboarding
- Incident response
- Architecture decision governance
- OSS intake and supply-chain governance
- AI-assisted change delivery

## Current Strengths

- Templates are already file-based.
- JSON is already the practical source used by the loader.
- Shared roles, artifacts, checklists, validations, and prompts already exist.
- Process-local resources already exist.
- Templates already include branch outcomes, subprocess keys, artifact expectations, and artifact inputs.

## Current Gaps

- No explicit schema version per component.
- No content hash per component.
- No component base reference for local overrides.
- No override patch model.
- No three-way conflict detection.
- No migration chain.
- Markdown and Mermaid sidecars risk becoming stale.
- Current-module projection files couple source templates to the old implementation.

## Target File Layout

```text
Templates/Processes/
  pack.json
  migrations/
    0001-initial.json
    0002-add-step-strategy-refs.json
  components/
    roles/
    artifacts/
    steps/
    branches/
    managers/
    recovery-policies/
    monitoring-profiles/
  processes/
    software-delivery/
      process.json
      overrides/
      generated/
```

Generated files under `generated/` are cache/export material, not source of truth.

