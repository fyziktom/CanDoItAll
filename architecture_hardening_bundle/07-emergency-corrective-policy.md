# Emergency corrective policy

## Why this exists

The repository already contains examples of good phased bundle execution, but this bundle raises the standard by making review-gate failure handling explicit and mandatory.

## Naming convention

When a review gate fails, create a corrective subbundle in one of these forms:

- `04A-corrective-<short-topic>`
- `07A-corrective-<short-topic>`
- `11A-corrective-<short-topic>`
- `15A-corrective-<short-topic>`

The prefix must tie the corrective subbundle to the gate that failed.

## Mandatory corrective updates

Every corrective subbundle must update:

- `plan/01-phase-plan.md`
- `codex/MASTER_TASKS.json`
- `codex/TASKS.json`
- `traceability/01-requirement-traceability.md`
- `reviews/01-execution-report.md`
- `reviews/02-architecture-gate-memo-log.md`

## Corrective content requirements

Every corrective subbundle must contain:

- root cause,
- exact impacted files and tests,
- why the previous gate failed,
- the smallest valid correction,
- rerun commands,
- unblock condition,
- closure evidence.

## Prebuilt corrective playbooks

This bundle includes four corrective playbooks:

- `subbundles/_corrective-foundation-stabilization`
- `subbundles/_corrective-persistence-and-concurrency-reset`
- `subbundles/_corrective-runtime-and-query-reset`
- `subbundles/_corrective-workspace-and-shared-infrastructure-reset`

Use the closest playbook when the failure clearly matches. Otherwise use `subbundles/_corrective-template`.

## Unblock rule

No downstream subbundle may start until:
1. the corrective subbundle is complete,
2. the failed gate is rerun,
3. the rerun result is recorded as `Passed`.
