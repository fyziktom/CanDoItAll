# CanDoItAll Codex Architecture Review

This directory documents the architecture-review assets retained in the checked-in compatibility mirror. Canonical CanDoItAll skills are maintained in `CanDoItAll.SharedInfo`; see [Codex skills](../README.md) for ownership and installation. Do not edit or install this mirror as a second source of truth.

The review workflow covers **canonical model integrity** and **architecture stabilization** in a project-operating-system style codebase.

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

The retained mirror paths are:

- `codex/skills/architecture-reviews/...`
- `.codex/agents/...`
- `codex/architecture-review/...`

If these assets were imported from a one-off package folder, that package copy is source material only after the merge and should not be treated as a second active tooling location.

Install or refresh the canonical SharedInfo skills into your local Codex home with:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1
```

If you only want the canonical CanDoItAll skills without refreshing public sibling skills, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\codex\scripts\install-candoitall-skills.ps1 -SkipPublicSkills
```

The installer requires the sibling `CanDoItAll.SharedInfo` repository unless `-SharedInfoRepoRoot` points elsewhere. If Codex does not notice refreshed skills or repo-level custom agents immediately, restart Codex.

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

## CodeAnalytics First

These skills are written to **prefer CanDoItAll CodeAnalytics MCP** for:

- solution graph inspection
- symbol navigation
- project reference analysis

SharpTools is backup-only and should remain disabled by default. Enable it only if CodeAnalytics has a real unresolved capability gap that cannot be repaired during the run.

## Optional review output location

The helper script can create a timestamped review folder such as:

- `architecture/reviews/2026-04-03-canonical-model-review/`
- `architecture/reviews/2026-04-03-feature-block-review/`

This is optional but recommended so recurring reviews become comparable over time.

## Historical Customization Reference

The paths below show where the retained package kept customization inputs:

- `codex/architecture-review/AGENTS.review-snippet.md`
- `codex/architecture-review/config.review-snippet.toml`
- `codex/skills/architecture-reviews/*/agents/openai.yaml`
- report templates inside each skill's `assets/`

Do not customize this compatibility mirror. Make maintained skill or agent changes in the canonical `CanDoItAll.SharedInfo` source and refresh the local installation through the repository installer.

## Notes

- The skills are written in **English** on purpose so Codex can follow them consistently.
- The review output should stay **evidence-backed**. The skill is intentionally skeptical and tries to separate real domain truth from projections, UI state, runtime state, or AI-generated proposals.
