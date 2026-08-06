# Canonical model review template

## Scope and evidence

## Concept classification

| Concept | Primary kind | Owner | Identity | Mutation authority | Persistence | Derived from | Risk |
|---|---|---|---|---|---|---|---|
| Product project/task data | Canonical entity/value graph | Owning product module | Product IDs | Product application service | PostgreSQL/workspace persistence | — | |
| Gantt view | Projection/UI state | Workbench | Project + view state | UI/product commands | optional view state only | Project Structure | |
| Live UI observation | UI/runtime state | Context registry | scope/version | active module publisher | ephemeral | current UI | |
| Conversation binding | Chat context state | Conversation context store | chat/session + epoch | chat coordinator | durable safe metadata | prior binding + current observation | |
| Turn context | Runtime snapshot | turn capture service | capture ID/digest | immutable after admission | safe reference + lease | observation + authority | |
| Execution authority | Policy/authorization object | authority resolver | authority ID/fingerprint | canonical policy services | safe record | principal/agent/product grants | |
| Execution run | Runtime canonical state | execution store | run ID | execution coordinator | durable | admitted command/runtime evidence | |
| MAF state | Integration adapter state | MAF state adapter | adapter/schema envelope | MAF adapter | opaque envelope | provider/framework session | |
| Lightweight LLM invocation | Stateless integration/application operation | LLM invocation port + provider adapter | operation/correlation ID | caller supplies immutable request; provider adapter maps only | usage/evidence may be persisted by caller | ordered messages + explicit settings | |
| Ordinary LLM conversation | Application conversation state | future LLM conversation service/store | conversation ID | ordinary-chat application service | dedicated transcript store | prior transcript + explicit user turn | |
| Agent conversation | Agent application/runtime state | agent workspace/chat services | agent + chat session ID | agent execution coordinator | agent chat/run stores | agent definition + transcript + execution state | |

## Source-of-truth questions

1. Can a UI projection mutate canonical truth except through typed application commands?
2. Can observation metadata widen execution authority?
3. Can a previous context epoch be mistaken for current facts?
4. Can MAF session state override application approvals or run state?
5. Can process artifact text promote a process outcome outside ordinary completion gates?
6. Can workflow payload data select authority?
7. Can lightweight inference construct or inherit an agent, tool, memory, context, approval, finalizer, or workspace graph?
8. Is provider runtime state treated as integration evidence rather than the canonical ordinary-chat transcript?
9. Does a future ordinary conversation own transcript, compaction, and provider/model policy above the stateless invocation port?
10. Is optional product/UI context for ordinary chat explicit and independently authorized rather than ambient?
11. Can usage be counted twice between provider driver, lightweight adapter, workflow projection, or conversation aggregation?

## Findings

| Severity | Claim | Evidence | Why it matters | Stabilization action | Timing |
|---|---|---|---|---|---|

## Scorecard

Score 1–5 with evidence:

- source_of_truth_integrity:
- boundary_clarity:
- invariant_enforcement:
- projection_discipline:
- integration_isolation:
- runtime_state_separation:
- ai_policy_separation:
- testable_architecture:
- change_safety:
- overall_stability:

## Required closure evidence

- owner table for agent chat, ordinary LLM conversation, stateless invocation, provider runtime state, and MAF state;
- source/dependency proof that these states cannot overwrite one another;
- restart fixtures for agent continuation and future-facing ordinary-chat contract assumptions;
- usage-accounting ownership map;
- explicit statement of what this bundle deliberately does not persist or expose.

## Decision

Status: Pass | Blocked | Pass with bounded follow-up
