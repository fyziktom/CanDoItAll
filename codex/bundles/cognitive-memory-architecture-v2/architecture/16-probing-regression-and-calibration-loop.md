# Probing Regression And Confidence Calibration Loop

## Purpose

Interactive probing should not only reveal one-off mistakes. It should create a durable improvement loop that prevents the same memory failure from returning. The loop has two outputs:

1. **Regression tests** for repeated recall/answer behavior.
2. **Confidence calibration records** for tuning how strongly the system trusts its own answers.
3. **Self-regulation training evidence** for posture, humility triggers, known failure patterns, and confidence reinforcement.

## Why This Is Needed

A memory system can fail in subtle ways:

- it retrieves a semantically similar but wrong context,
- it answers confidently without enough source evidence,
- it cites a stale record,
- it hides uncertainty behind a fluent summary,
- it forgets a rarely used but high-risk procedure,
- it merges two similar topics that should remain separate.

Static tests catch only known examples. Probing generates new examples naturally from conversation.

## Regression Test Case Model

A memory regression test should store:

| Field | Meaning |
|---|---|
| `Question` | The user/system question to replay. |
| `ProjectId` | Scope where the answer is expected. |
| `Mode` | Recall/probe/learning validation mode. |
| `ExpectedConstraints` | Must-have answer facts, uncertainty statements, relation behavior. |
| `RequiredSourceRefs` | Source/memory ids that must be used when available. |
| `ForbiddenSourceRefs` | Related but wrong-context sources that must not become authoritative. |
| `ForbiddenClaims` | Claims that caused a prior failure. |
| `AccessContext` | Role/scope constraints used during replay. |
| `EvaluatorProfile` | Deterministic, LLM-assisted, or human-reviewed evaluator mode. |
| `CreatedFromProbeTurnId` | Link back to the failing conversation. |
| `State` | Draft, active, passing, failing, retired, needs review. |

## Evaluator Modes

| Mode | Use |
|---|---|
| Deterministic | Required/forbidden ids, states, tags, source refs, and warnings. |
| Heuristic | Text constraints, uncertainty phrases, trace shape, score vectors, shape matches, and scalar projection ranges. |
| LLM-assisted | Complex semantic answer quality; must be reviewable and not sole proof for high-risk tests. |
| Human-reviewed | Critical procedures, policy, security, finance, compliance, or destructive automation. |

## Confidence Calibration

Every probe answer should compare confidence against outcome:

| Pattern | Meaning |
|---|---|
| High confidence + confirmed | Confidence model is probably calibrated. |
| High confidence + corrected | Dangerous overconfidence. Penalize feature pattern and create review evidence. |
| Low confidence + confirmed | System may be too hesitant; source coverage may be better than estimated. |
| Low confidence + missing | Gap model is aligned. |
| Medium confidence + wrong scope | Context-separation score shape or projection needs adjustment. |

Calibration records should not immediately change canonical truth. They should tune score-space definitions, shape thresholds, answer confidence display, and future question generation through reviewable profile/version changes.

## Calibration Inputs

- Final answer confidence projection and answer-gate evaluation trace.
- Self-regulation assessment and answer posture decision.
- Recall candidate score vectors and shape matches.
- Number and quality of source refs.
- Validation state of memory items.
- Staleness and contradiction pressure.
- User feedback outcome.
- Whether answer used a generated summary or raw source-backed item.
- Whether important memory was excluded by budget.
- Whether access policy redacted key evidence.

## Integration With Recall Scoring

The recall orchestrator should expose feature-level score dimensions, matched shapes, and scalar projections. Calibration needs to know whether the failure was caused by:

- semantic similarity overpowering scope/graph separation,
- low activation of a correct dormant record,
- stale records being selected,
- budget excluding a required detail,
- vector projection outage,
- lexical fallback selecting a generic answer,
- access policy correctly hiding restricted data.

## Docker Context-Separation Regression Pack

The first regression pack should cover:

1. Production Docker deployment.
2. Test/simulation Docker deployment.
3. Local development Docker Compose.
4. CI Docker test pipeline.
5. Unrelated UI testing.

Required behavior:

- All Docker records may be semantically related.
- Production/test/local/CI contexts must remain separable.
- Production recall must not use test-only settings as authoritative.
- Test simulation recall must prefer test-specific procedure memory.
- Project summary may mention all contexts while preserving distinctions.

## Persistence Rule

Regression tests are durable memory quality artifacts. They should be stored in relational state and optionally projected for search. Qdrant may help find tests, but it is never the authoritative test store.

## MVP Acceptance

- A failed probe turn can create a draft regression test.
- A reviewer can activate the test.
- Test replay runs recall and stores a result.
- The result links to recall trace and probe turn.
- The result can create review/gap evidence without mutating truth directly.
- The Docker fixture proves context separation.

## Neuro-Cognitive Regression And Calibration Updates

Regression replay is a `MemoryReplayJobRecord` kind. The replay scheduler, not ad hoc test code, should decide when probe regressions are repeated because of salience, prediction error, risk, staleness, or repeated use.

Regression tests should store expected claim-level constraints:

- required claim ids,
- forbidden claim ids,
- required context frames,
- forbidden context substitutions,
- required evidence anchors,
- required answer-gate decision or warning,
- required uncertainty statement.

Calibration records feed the metamemory answer gate. A pattern of overconfident wrong-scope answers should cause future answers with similar context boundaries to warn, clarify, probe, or abstain before rendering.

Calibration records also feed Self-Regulation. They can update calibration health aggregates, known failure pattern proposals, humility triggers, confidence reinforcement records, professor-review routing, and answer posture thresholds through versioned profile changes.

Replay and calibration output can create signals, prediction errors, review items, replay jobs, probing drills, self-model update proposals, and mutation candidates. They cannot directly promote truth.
