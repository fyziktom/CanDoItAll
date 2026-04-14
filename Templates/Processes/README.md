# CanDoItAll process template pack

This pack is the current-architecture-aligned replacement for the original execution bundle.

## Goals
- Keep process templates file-driven and outside compiled C# code.
- Preserve shared and local resource sidecars for roles, artifacts, checklists, validations, prompts, and step documents.
- Project current-module import envelopes with first-class dependencies, artifact inputs, decision roles, and branch outcomes.
- Provide Mermaid exports plus supporting markdown files for human inspection and downstream tooling.

## Current architecture adjustments
- Added explicit process-level role usages.
- Added first-class step dependencies and artifact-input definitions.
- Added a new branching-code-review template aligned to the current baseline scenarios.
- Realigned the baseline scenario catalog to five scenarios matching the current repository expectations.
- Added corrective guidance for the remaining hardcoded authoring chrome in ProcessCanvasSurfaceFactory.

## Folder layout
- `shared/` contains reusable roles, artifacts, checklists, validations, and prompts.
- `processes/<key>/` contains the template definition, local resources, step docs, Mermaid exports, and projection sidecars.
- `toolbox/` contains role/step seeds and the proposed chrome-action catalog.
- `seed-catalog/` contains baseline seeded runtime scenarios.

## Validation
Use `tools/validate_process_template_pack.py` to validate JSON references, dependency graphs, artifact inputs, and current baseline expectations.
