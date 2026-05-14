# enterprise-wiki-and-infographics

## Status

- `Completed`

## Objective

- Add customer-facing documentation and four project-local enterprise infographic assets.

## Success Criteria

- `docs/enterprise-operating-system.md` explains why and how enterprise users should use CanDoItAll.
- Four generated PNGs are saved under `docs/images`.
- The doc references each infographic with captions and alt text.
- Process concepts, escalations, observation, HR matching, validation, audit, and Economy direction are covered without overclaiming.

## Covered Inputs

- `REQ-004`
- `REQ-005`
- `REQ-006`
- `REQ-007`

## Prerequisites

- `subbundles/01-architecture-api-doc-refresh` closure gate passed.

## Exact Source References

- C:/repositories/CanDoItAll/README.md
- C:/repositories/CanDoItAll/docs/README.md
- C:/repositories/CanDoItAll/Templates/Processes/README.md
- C:/repositories/CanDoItAll/docs/process-agent-operator-runbook.md
- C:/repositories/CanDoItAll/docs/agent-output-contracts.md
- C:/repositories/CanDoItAll/codex/skills/candoitall-api-processes/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/candoitall-api-project-structure/SKILL.md
- C:/repositories/CanDoItAll/codex/skills/candoitall-api-agents/SKILL.md

## Deliverables

- New customer-facing `docs/enterprise-operating-system.md`.
- `docs/images/candoitall-executive-summary.png`.
- `docs/images/candoitall-technical-manager.png`.
- `docs/images/candoitall-everyday-manager.png`.
- `docs/images/candoitall-technical-specialist.png`.
- Updated docs index links.

## Dependency Impact

- Final validation depends on this phase for image-file existence and customer-doc link integrity.
- README/docs index usefulness depends on this phase being discoverable.

## Validation Depth

- Customer-facing documentation and static asset proof.

## Implementation Steps

1. Generate four audience-specific raster infographics using `imagegen`.
2. Move selected images into `docs/images` with stable names.
3. Write `docs/enterprise-operating-system.md` with audience sections and process explanations.
4. Reference all four images from the new doc and docs index.
5. Keep authoritative technical claims in Markdown captions, not only image text.

## Scope Exceptions

- No external `CanDoItAll.Economy` repo inspection is in scope; Economy is documented only from the user's provided context.

## Do Not Do

- Do not generate one overloaded infographic.
- Do not put too much precise text inside images.
- Do not describe Economy as shipped in this repository.
- Do not add marketing-only landing-page copy at the expense of practical adoption guidance.

## Acceptance Checklist

- Four image files exist in `docs/images`.
- Customer doc has separate sections for executives, technical managers, everyday managers, and technical specialists.
- Customer doc explains Plan, Execute, Validate, Audit.
- Customer doc explains escalations, observation manager, HR/agent matching, and audit trail.
- Economy direction is described as external/adjacent private-ledger work.

## Proof Required

- Image file listing under `docs/images`.
- File inspection of `docs/enterprise-operating-system.md`.
- Final `git diff --check` in subbundle 03.

## Browser Validation Logging

- N/A: generated image assets are static documentation files, not app UI behavior.

## Progression Gate

- Downstream validation may continue only after all four expected image files exist and the customer doc references them.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
