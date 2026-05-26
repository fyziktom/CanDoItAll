# SB05: Blazor WASM PWA And Browser Proof Skills

## Status

- Status: `Completed`

## Objective

Add or upgrade reusable skill guidance for generic Blazor WASM PWA delivery and browser proof.

## Covered Inputs

- RQ05 reusable Blazor/browser/project-structure/process proof skills.

## Prerequisites

- SB04 role/tool matrix is complete.

## Exact Source References

- `repo://codex/skills/candoitall-api-processes/SKILL.md`
- `repo://codex/skills/candoitall-watch-playwright-loop/SKILL.md`
- `repo://Templates/Agents/teams/delivery-platform`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`

## Deliverables

- Generic Blazor WASM PWA delivery guidance covering project structure, PWA manifest/service worker, static assets, UI state, route validation, build/test, and browser proof.
- Process API skill examples that use neutral app names and app-topic placeholders.
- Active skill-root synchronization proof if a repo skill used by Codex is changed.

## Dependency Impact

- SB06 and later subbundles depend on updated reusable skills to avoid topic-specific instructions.

## Validation Depth

- Source assertion and skill synchronization proof if active skill content changes.

## Implementation Steps

1. Audit process and browser-proof skills for app-topic-specific examples.
2. Replace examples with generic Blazor WASM PWA app delivery placeholders.
3. Add instructions for topic-specific criteria to come from project structure or run prompt.
4. Record repo/active skill hashes if synchronization is required.

## Do Not Do

- Do not encode a demonstration topic or domain-specific route into a reusable skill.
- Do not treat generated images or screenshots as proof unless they are captured from the shipped app.

## Acceptance Checklist

- Skill text is generic and reusable for any Blazor WASM PWA topic.
- Browser proof requirements remain concrete.
- Project-structure writeback is documented as controlled external action.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- `proof/SB05/transcripts/source-assertions.txt`
- `proof/SB05/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A. This subbundle changes skills and proof instructions.

## Progression Gate

- SB06 may start after skill text is generic and synchronization proof is recorded when applicable.

## Suggested Agent Prompt

Update reusable Blazor WASM PWA and browser proof skills so they are generic, concrete, and ready for any app topic.
