# Tighten execution closure and MTP hot-reload rules

## Status

- `Completed`

## Objective

- Tighten the workflow and execution rules so bundle status stays synchronized through delivery and `mtp-hot-reload` is treated only as an iteration accelerator with a mandatory clean proof rerun.

## Covered Inputs

- `F003`
- `F004`
- `R003`
- `R004`
- `R005`

## Exact Source References

- `C:\Users\lucys\.codex\skills\candoitall-bundle-workflow\SKILL.md`
- `C:\Users\lucys\.codex\skills\candoitall-bundle-execution\SKILL.md`
- `C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\SKILL.md`
- `C:\Users\lucys\.codex\skills\mtp-hot-reload\SKILL.md`

## Deliverables

- workflow and execution instructions that require final root README status synchronization
- explicit validator rerun requirement after material bundle repair
- sharper wording that hot-reload output is iteration-only and standard proof must still run cleanly

## Implementation Steps

1. Update the workflow skill with explicit end-of-run documentation closure and validator-loop requirements.
2. Update the execution skill with the same closure rule at subbundle and final-bundle level.
3. Refresh preparation wording where needed so `mtp-hot-reload` references stay consistent across all bundle skills.

## Scope Exceptions

- none

## Do Not Do

- do not make `mtp-hot-reload` mandatory
- do not treat a documentation-only status refresh as optional after proof lands
- do not introduce repo-specific command examples that assume Microsoft Testing Platform when the repo uses VSTest

## Acceptance Checklist

- all three bundle skills describe `mtp-hot-reload` as optional and MTP-gated
- workflow and execution skills explicitly require final root README and execution-report status synchronization
- workflow or execution instructions explicitly require validator reruns after material bundle repair

## Proof Required

- re-read the updated skill files and confirm the new rules are present
- rerun the validator on `candoitall-bundle-skills-improvements-1` after the skill edits are complete

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Tighten the workflow text so future bundle runs cannot finish with stale README/report status text or with hot-reload-only proof. Keep mtp-hot-reload optional, MTP-gated, and explicitly non-final.
```
