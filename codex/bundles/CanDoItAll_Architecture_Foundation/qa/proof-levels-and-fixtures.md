# Proof levels, fixtures, and acceptance

## What this package proves

Document/cross-reference integrity and a recorded architecture/QA review. **Not application build, migrations, tests, browser behavior, or live runtime.** Missing environment is BLOCKED/NOT_RUN, not a mock-based PASS.

| Level | Can prove | Cannot prove alone |
|---|---|---|
| Static graph/model review | Actual references, EF model types, leaked types, known writers | Commit/concurrency or real provider behavior. |
| Unit | Invariants, normalization/fingerprints, units, state transitions | PostgreSQL locks/FK/cascades or durable effects. |
| Owner integration | Commands against real persistence and policies | Floating UI or live LLM transport. |
| Cross-owner integration | Transactions, adapters, auth, outbox/receipts, restart | Every dialog/render detail. |
| Component/fake host | Rendering, state, focus, subscriptions, stale-load cancellation | Production persistence/execution/authority. |
| Desktop browser | Floating window, Gantt/dialog/route and actual user adapter | Every backend/OS/recovery point. |
| Live adapter smoke | Actual supported provider/executor/FileTools connection | Complete unit/integration coverage. |
| Migration/restore rehearsal | Upgrade, IDs/history, old payloads, recovery/rollback | Full business and UI sign-off. |

Do not test partial-class/interface/file counts, bundle names, or line counts. Test authority, actual dependency/model boundaries, invariants, and truthful outcomes. Correct alternative internal decompositions should pass.

## Synthetic reference data

Use projects A/B, a second allowed scope with equal names, and where supported a second profile with colliding local IDs. Create human parties with different privacy/roles; technical agent plus one CRM binding; agent without enrollment, inactive agent, and template. Include explicit free/paid/unknown prices without live secrets.

Structure needs an inline note, task/date/dependency/assignment, file whose Notes differ from bytes, shared content with two bindings, projected agent, workflow/process references, and output folder. Deterministic workflow reads/creates assets/tasks with pinned version. Process has child lineage, a confirmed effect, waiting step, and recovery point. Unique content hashes make concurrent-output misattribution detectable.

Also use CRM opportunity/recruitment data, prompt versions, Resource connector/content, TestLab evidence, Scheduler firing, Collaboration thread, ordinary Simple Chat, and Memory source. Use actual current model/schema; this is not permission to add production seed data.

For this revision add managed HR, an ordinary query-only agent, optional separately configured CRM specialist fixture, forbidden-capability targets, safe/confidential CRM canaries, two same-name parties, Simple Chats definition/revision plus unrelated conversations, allowed/disallowed provider-disclosure profiles, and durable cross-owner effect/checkpoint fixtures. Use fake provider transport for canary assertions and separately authorized live smoke for actual transport; never send real secrets or private personnel data to a test provider.

## Baseline capture and selection

Capture current SHA, schema/migrations, host/profile, concrete API/tool/executor registrations and supported purposes. Discover actual tests; zero or unexpectedly changed selection is a gate issue. This foundation does not freeze volatile test names/counts/commands.

For each affected journey capture input, owner receipt/ID/revision/effect evidence and relevant browser focus/route state, sanitized. Seed-created data is not proof a user/agent can execute the operation. For new surfaces separately prove attachment/denial, approval, headless delegation, owner commit, and checkpoint recovery.

Run affected owner/contract tests and relevant end-to-end stories rather than every UI test for every small slice. Shared authority, profile lifecycle, serializers, storage policy, or schema changes trigger broader gates. Do not omit an affected real cross-module route merely for speed.

## Status

PASS requires actual successful execution and evidence. FAIL cannot be excused by architectural cleanliness. BLOCKED/NOT_RUN is not PASS. A baseline gap requires evidence and an explicit decision whether scoped hardening covers it; never widen it or quietly remove its test surface.

Future features are not an endless release gate. Gates concern supported behavior, guarantees introduced by the chosen slice, and affected invariants. A newly introduced mutation cannot advertise safe retries until its atomic owner-receipt proof exists. QA-165 is a package/downstream language inspection; its specification remains distinct from the mechanical package-check report.
