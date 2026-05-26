# Target Solution

## Architecture Intent

- Keep the Processes runtime generic and PostgreSQL-only; Tetris and Blazor WASM PWA details belong in templates, baseline scenarios, launch profiles, documentation, and tests.
- Treat typed operation contracts as the authoritative policy surface for process steps, API/tool payloads, template projection, dispatch metadata, and validation.
- Route manual/API step completion through the same artifact-validation semantics used by automation finalization.
- Make project-structure mutation tools explicit governed external actions, not ambient product mutation.
- Keep Blazor UI components focused on orchestration and visibility; move non-trivial runtime validation logic into services.

## Boundaries

- UI: Blazor components and pages under `repo://src/CanDoItAll.Modules.Processes/Components` and `repo://src/CanDoItAll.Modules.Processes/Pages`.
- Application/runtime services: process services, dispatch services, validation services, template services, and read-model projectors under `repo://src/CanDoItAll.Modules.Processes`.
- Domain contracts: definitions, operation contracts, target scopes, recovery enums, and artifact mapping fields under `repo://src/CanDoItAll.Modules.Processes/Definitions`.
- Infrastructure: EF persistence, PostgreSQL migrations, API endpoints, MAF tools, and skill/documentation surfaces.

## Validation Strategy

- Start with a failing-first or adversarial proof for each behavior change.
- Close each critical subbundle with `bundle://proof/SBxx/manifest.md`, `bundle://proof/SBxx/semantic-invariants.md`, command transcripts, changed-file hashes, source assertions, anti-stub audit, and downstream smoke proof when required.
- Use Playwright/browser proof only for UI-visible changes and the Tetris UI preflight; template-only subbundles record N/A browser analytics unless they change rendered UI.
