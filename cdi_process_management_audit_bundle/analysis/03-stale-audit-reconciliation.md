# Stale Audit Reconciliation

## Why The Original Audit Bundle Could Not Be Executed Directly

- The original package treated a stale broad audit as if it were an implementation contract.
- The legacy backlog mixed already-landed foundations with still-open gaps, so blindly executing it would have duplicated work and hidden live priorities.
- The original package had no operational phase gates, no validator-compliant structure, and no subbundle ownership for proof.

## Legacy Findings Reopened In This Bundle

- The legacy claim that branch semantics are missing is still true in the live repo.
- The legacy claim that runtime progression still uses sequence ordering is still true in the live repo.
- The user explicitly reinforced both gaps with the branching requirement, so those findings are reopened as the critical path.

## Legacy Findings Not Reopened Blindly

- Decision records, work briefs, artifacts, conformance observations, and improvement candidates already exist in the live runtime.
- MCP process tools already exist.
- Seed and integration scaffolding already exist.
- Those areas are still subject to regression checks where touched, but they are not reopened as stand-alone implementation phases in this bundle without live defect evidence.

## Narrowing Rule Applied Here

- This bundle does not pretend the whole legacy 15-task roadmap is complete.
- It explicitly converts the stale roadmap into three buckets:
- Already present in the live repo.
- Still partially open but not necessary to solve the proven branching defect in this run.
- Reopened now because the live code and the user request both prove they are still missing.
- The explicit bucket decisions live in `inventories/02-legacy-backlog-disposition.md`.
