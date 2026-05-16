# 24 Metamemory Confidence And Abstention

## Purpose

Add an answer-time metamemory gate that decides whether the system should answer, answer with warnings, ask for clarification, request source audit, start probing, propose learning, or abstain.

The existing architecture stores confidence and calibration evidence. This patch makes those signals operational before answers leave the system.

The answer gate now consumes Cognitive Self-Regulation. Self-Regulation selects the posture and required operations from self-model, calibration health, failure patterns, humility triggers, risk, evidence, and workspace state. The answer gate remains the final answer-time boundary.

## Metamemory Questions

Before answering, the system should evaluate:

1. Do we have source-backed claims for this answer?
2. Are the claims valid for the requested context?
3. Are there unresolved contradictions?
4. Is the source stale or redacted?
5. Is the confidence calibrated for this type of question?
6. Is the procedure high-risk or potentially destructive?
7. Is the user asking for a scope that is ambiguous?
8. Would a fluent answer hide uncertainty?
9. Should we ask a clarifying question instead?
10. Should this be a probe, learning proposal, or review item?
11. Did self-regulation require a posture or operation that this answer must respect?
12. Did final answer synthesis introduce new unsupported claims that require a stricter decision?

## Answer Gate Decisions

| Decision | Meaning |
|---|---|
| `Answer` | Source support and context fit are sufficient. |
| `AnswerWithWarnings` | Answer is useful but includes caveats. |
| `AskClarification` | Context/intent/scope is ambiguous. |
| `RequestSourceAudit` | Sources are insufficient, stale, redacted, or disputed. |
| `StartProbe` | Topic should be tested interactively before study/change. |
| `CreateReviewItem` | Risk or contradiction requires review. |
| `RequestLearningProposal` | Knowledge is missing and relevant. |
| `Abstain` | Answer would be misleading or unsafe. |

## Gate Inputs

- recall result,
- recall trace,
- workspace frame,
- selected claim belief states,
- source anchors,
- contradiction pressure,
- redaction state,
- context-boundary decisions,
- confidence calibration records,
- access policy,
- risk category,
- user/agent/process role,
- procedure maturity.
- self-regulation assessment,
- answer posture decision,
- professor review requirement/result when applicable.

## Gate Output

The gate should output:

- decision,
- answer policy,
- warnings,
- source sufficiency summary,
- score-geometry evaluation trace,
- derived confidence/certainty projection,
- required next actions,
- blocked claims or candidates,
- audit metadata for trace.

The gate must use the shared `AnswerGate` score space. Abstention and warning behavior should come from matched shapes such as source-poor high-risk answer, contested claim, redaction-limited source, ambiguous context, low procedure maturity, or poor calibration. Display confidence is a rendering aid only.

If self-regulation selected `ClarifyingQuestion`, `SourceAuditRequest`, `ProbeQuestion`, `ReviewRequired`, `ProfessorReviewRequired`, or `Abstain`, the answer gate must enforce that posture unless it performs a new score-geometry evaluation proving the constraint is resolved. The gate may become stricter than self-regulation when final synthesis introduces new source insufficiency, contradiction, redaction, risk, or uncertainty. It must not become looser without a new trace.

## Relationship To Probing

When the gate detects uncertainty that can be efficiently resolved by interrogation, it should suggest a probe session or a probe question set.

Examples:

- high-confidence but historically error-prone feature pattern,
- wrong-scope risk,
- user repeatedly asks adjacent questions,
- missing source anchors for an important claim,
- procedure maturity too low for automation.

## Relationship To Learning

The gate should not automatically start external learning. It can request a learning proposal when:

- knowledge is missing,
- the topic is relevant to active project direction,
- approved sources may exist,
- probing cannot resolve the gap cheaply.

## Rendering Rules

Answer renderer should clearly separate:

- source-backed claims,
- generated synthesis,
- assumptions,
- uncertainty,
- stale or contested points,
- context boundaries,
- next recommended action.

## Required Tests

- high-confidence but attacked claim produces warning or abstention,
- missing source anchor triggers source audit rather than confident answer,
- ambiguous production/test Docker question asks clarification,
- low-risk preference question can answer with lower evidence threshold,
- high-risk procedure requires validation or review,
- self-regulation required operation is enforced by answer gate,
- answer gate cannot become looser than self-regulation without a new trace,
- answer gate result is included in recall/probe trace.
