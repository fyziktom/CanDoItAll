# CanDoItAll Codex Architecture Review

This repo now ships a repeatable Codex review workflow for **canonical model integrity** and **architecture stabilization** in a project-operating-system style codebase.

## What is included

### Skills (`codex/skills/architecture-reviews`)
1. **canonical-model-review**
   - Deep review of source-of-truth boundaries, domain concepts, invariants, projections, integrations, runtime state, and agent/AI-related contamination of the canonical model.
2. **feature-block-architecture-review**
   - Focused review to run after a larger feature/module/block lands.
3. **architecture-drift-audit**
   - Lightweight recurring audit for architecture drift, layering erosion, and emerging hotspots.

### Optional custom agents (`.codex/agents`)
1. **arch_mapper**
   - Read-only explorer for solution structure, boundaries, and canonical-model candidates.
2. **canonical_model_skeptic**
   - Read-only skeptic focused on source of truth, invariants, and projection leakage.
3. **runtime_validator**
   - Safe validator for builds, targeted tests, and startup checks. It does not edit code.

### Helper assets
- Report templates
- Scorecard templates
- ADR template
- Review checklists
- Suggested `AGENTS.md` snippet
- Minimal helper scripts

## Repo layout

The canonical repo paths are:

- `codex/skills/architecture-reviews/...`
- `.codex/agents/...`
- `codex/architecture-review/...`

If these assets were imported from a one-off package folder, that package copy is source material only after the merge and should not be treated as a second active tooling location.

Install or refresh the repo-managed skills into your local Codex home with:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1
```

If you only want the repo-managed skills without refreshing public sibling skills, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1 -SkipPublicSkills
```

If Codex does not notice the new skills or repo-level custom agents immediately, restart Codex.

## Suggested usage

### After adding a larger block/module
Use:

- `$feature-block-architecture-review`

Suggested prompt:

> Use $feature-block-architecture-review on the recent CRM/HR block. Focus on canonical model fit, boundary clarity, and what must be stabilized before the next feature wave.

### Before the next feature wave or before a release
Use:

- `$canonical-model-review`

Suggested prompt:

> Use $canonical-model-review on this repo. Treat it as a project operating system. Focus on the canonical model, source-of-truth boundaries, projections vs entities, and architecture stabilization priorities.

### Periodic health check
Use:

- `$architecture-drift-audit`

Suggested prompt:

> Use $architecture-drift-audit. Scan for architecture drift since the last review and produce a prioritized list of hotspots with stabilization recommendations.

## Suggested cadence

- **After every major block**: feature-block-architecture-review
- **Before each new feature wave**: canonical-model-review
- **Before release / monthly**: architecture-drift-audit

## SharpTools MCP

These skills are written to **prefer SharpTools MCP when it is available** for:

- solution graph inspection
- symbol navigation
- project reference analysis
- targeted test execution
- startup and runtime evidence gathering

If SharpTools is not available, the skills still work with standard shell / repo inspection.

## Optional review output location

The helper script can create a timestamped review folder such as:

- `architecture/reviews/2026-04-03-canonical-model-review/`
- `architecture/reviews/2026-04-03-feature-block-review/`

This is optional but recommended so recurring reviews become comparable over time.

## Files you may want to customize

- `codex/architecture-review/AGENTS.review-snippet.md`
- `codex/architecture-review/config.review-snippet.toml`
- `codex/skills/architecture-reviews/*/agents/openai.yaml` if you want to add or rename specific MCP tool dependencies
- report templates inside each skill's `assets/`

## Notes

- The skills are written in **English** on purpose so Codex can follow them consistently.
- The review output should stay **evidence-backed**. The skill is intentionally skeptical and tries to separate real domain truth from projections, UI state, runtime state, or AI-generated proposals.
