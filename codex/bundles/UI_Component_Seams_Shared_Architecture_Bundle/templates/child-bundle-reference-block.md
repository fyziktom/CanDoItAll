## Shared UI Component Seam Architecture Base

This bundle is governed by:

- Reference ID: `CDA-UI-SEAMS-BASE-v1`
- Expected repository path:
  `codex/bundles/UI_Component_Seams_Shared_Architecture_Bundle`
- Base kind: non-executable shared architecture reference
- Base version: `1`

The base does not supply this bundle's source scope, implementation steps, test commands,
or proof. This child bundle owns all of them.

### Applicable base rules

- [ ] Preserve component location during logical seam extraction unless relocation is an
      explicit outcome.
- [ ] Keep `AppComponents` independent from concrete feature modules.
- [ ] Classify state and move route-significant ownership to the page/workspace.
- [ ] Use the smallest real abstraction; avoid wrapper/interface inflation.
- [ ] Remove hidden service location and direct persistence access from Razor.
- [ ] Do not add partial files as the final architecture.
- [ ] Remove or rewrite incidental source-shape tests in the touched area.
- [ ] Record route, sandbox, and project-extraction readiness.

### Deviations

List any deviation from the base, the reason, owner approval, and effect on later bundles.
