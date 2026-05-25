# QA Prompt For Codex

You are reviewing the completed implementation of `processes-hardening-followup-runtime-resilience-v2`.

Do not accept source-assertion-only proof. Require behavior tests.

Ask:

1. Can an architecture/scope/review step mutate external product files?
2. Can it mutate managed output product files?
3. Can it still write managed process artifacts?
4. Can a Work step that creates a report/decision/plan be classified as artifact-only instead of product mutation?
5. Does workflow-backed completion project workflow outputs to process artifacts before finalizer validation?
6. Does subprocess parent projection require current child run lineage?
7. Does manager recovery validate artifacts created by the recovery execution run?
8. Does upstream materialization unblock downstream steps after the missing artifact appears?
9. Does repair/no-go branch routing apply only to review/approval/disposition steps?
10. Does malformed relative JSON fail validation?
11. Does repeated invalid evidence compress retries?
12. Does active running execution avoid finalization?
13. Is the linter integrated into process publish/start/readiness?
14. Are generic non-software processes covered?

Record proof under `proof/SBxx/`.
