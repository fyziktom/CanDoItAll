# Task 07: Harden templates and prompt wording

## Goal

Make QA branch decisions unambiguous for smaller models.

## `qa-validation.md`

Add a compact branch evidence matrix:

| Branch | Required evidence | Not allowed |
|---|---|---|
| `quality-accepted` | current-run restore/build/test receipts, app run, browser navigate/snapshot/screenshot/console, stop receipt, no deterministic gate failures, acceptance criteria satisfied | accepting from upstream claims or shell screenshot only |
| `repair-required` | failed validation receipt, deterministic product content defect, or browser-proven product defect | missing proof caused by QA omission |
| `Blocked` | unavailable/denied tool, product root inaccessible, policy/provider failure | product defects that implementation can repair |

## `qa-recheck.md`

Same pattern, with `repair-escalation` for unresolved defect after repair.

## `quality-repair.md`

Add:

- read runtime gate findings,
- repair every listed gate failure,
- remove starter routes/nav/sample data when scaffold gate failed,
- update tests/proof per acceptance criteria ids,
- rerun smallest relevant validation proof.

## Prompt builder wording

Change the generic wording that currently says:

> submit Blocked or the applicable repair branch with the concrete current-run tool failure evidence

New meaning:

- unavailable/denied/failing tool before branch decision => `Blocked`,
- product defect proven by current-run evidence => repair branch,
- proof missing because the agent did not run the tool => retry/incomplete step, not product repair branch.

## Acceptance

- Templates reduce ambiguity but do not contain runtime-only implementation details.
- No branch behavior depends only on prompt text; runtime gates still enforce it.
