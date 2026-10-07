# Native boundaries that must survive WB2

Use [S15–S18, S25–S28] and current repository contracts. The project is not a single
CRUD entity, and its reporting and graph metadata are not a second authorization system.

## Reporting is projection, not a transaction or a new ledger

ProjectManagerSummaryScopeResolver owns bounded descendant discovery and plan
preflight; ProjectManagerSummaryQueryService owns native report composition; existing
plan/snapshot calculators own arithmetic and coverage. The reports combine multiple
owners and capture exact time windows, not a newly guaranteed atomic database snapshot.
Do not promise one by wrapping the UI call or silently open a huge cross-owner SQL
transaction. Keep the existing concurrency/batching and hard limits.

Agent conversations, SimpleChats, standalone Workflow and root Process records keep
their attribution rules. A Workflow's inner calls must not be counted again as an
independent whole Process charge. Different known/estimated/future currencies remain
explicit. Do not invent conversion rates, populate omitted historical prices, or
initialize/rebuild production usage indexes merely to show a complete badge.

When an owned fixture legitimately needs the existing derived-index initialization,
record the initially partial result and the explicit maintenance step. Hash original
canonical records before/after. It is not a runtime feature change and not proof that
all historical reporting data was complete at entry.

## Project and actor context

Retain the project/profile/lifetime that the native load actually accepted. Do not
infer a valid mutation from a visible node, a report membership list, renderer flag
or a previous successful query. Existing backend authorization remains decisive at
the actual write/content owner. A removed and recreated project is not the same lifetime
because its public GUID matches. Old callbacks cannot target a new selection.

A canonical running host profile and a selected next-restart database choice remain
distinct. Connect to existing notification/accessor semantics; do not add selected-
profile subscriptions to installation-scoped owners indiscriminately. Report cache
retention is not permission to show prior actor/profile data after retirement.

## Outcomes and inherited native guarantees

Known precommit validation/refusal, a confirmed write, confirmed work followed by
readback failure and genuinely unknown outcomes remain distinguishable. Store exact
accepted task/node/run/receipt IDs before follow-ups. Cancellation retires the view;
it neither rolls back an already committed operation nor grants permission to replay.
Use existing owner facts. A small missing result property may justify a bounded owner
change with tests, not a generic receipt/idempotency framework.

Preserve WB1 assignment/pricing compensation [S07–S09], precise schema metadata and
raw input, Calendar read-only semantics and Scheduler finite-plan restart. Selection
buttons must continue to use the already-completed WB1 native task flows [S26]. Do not
undo those fixes while adapting event signatures or types.

## Concrete final non-regression

Use two projects and distinct actor/source contexts, including colliding display
names and deliberately reused fixture node identifiers where supported. Native
queries and denied actions must not cross those contexts. Verify exact expected
write admission, the actual registered tool schema, required approval arguments and
post-write records. Unsupported tool/capability paths fail honestly; do not add a
new tool just to manufacture a test scenario.

Retain whole-agent configuration, original provider model IDs/display mappings,
shared-provider routing and History content authorization. No test may temporarily
grant AllowAll or disable approval globally to reach a green result.
