# SB03 Semantic Invariants

## Invariant ID

- `SB03-GENERIC-BROWSER-CONTRACT`

## Raw Note Owned

- `N006`: process core must remain generic.
- `N007`: detail belongs in project structure, skills/instructions, or process step definitions.

## Shallow-Pass Trap

A generic process template that says "browser proof as applicable" but lets agents return only chat text or stale screenshots can still pass the old failure shape. That is not enough.

## Adversarial Negative Proof

Console and API-only process wording must not become browser-gated just because shared QA text mentions browser artifacts. The regression transcript proves those non-UI cases do not require browser tools.

## Semantic Positive Proof

When the template, project structure, or implementation evidence identifies a visible browser workflow, the process contract and seeded agents require current-run browser screenshot, snapshot or evaluate state output, console messages, URL/entrypoint, launch/cleanup receipts, and acceptance-state assertion.

## Anti-Stub Audit

`bundle://proof/SB03/evidence/anti-hardcoding-audit.txt` proves no Tetris-specific terms were introduced in process runtime, process templates, agent templates, or seed skill assets.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Template browser evidence contract | Software-delivery template pack | Process projection and QA step materialization | Loaded from repo templates and projected into process definitions | Non-UI wording remains ungated |
| Agent instruction browser proof contract | Agent template pack and inline skill seed assets | Default agent catalog refresh | Existing stale managed agents/skills refresh through seed version `2026-05-agent-template-teams-v12` | Chat-only and stale browser proof is rejected in instructions |

## Raw-Note Literal Closure

`N006` and `N007` are solved at the contract level: product-specific acceptance remains outside process runtime, while process definitions and agent instructions now demand the generic evidence shape required to catch invisible UI state and console failures.
