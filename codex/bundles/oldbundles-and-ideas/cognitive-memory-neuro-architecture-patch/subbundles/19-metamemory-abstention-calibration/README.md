# 19 Metamemory Answer Gate And Calibration

## Objective

Add an answer-time gate that uses confidence calibration, belief state, source sufficiency, redaction, context fit, risk, and policy.

## Inputs

- `architecture/24-metamemory-confidence-and-abstention.md`
- Existing probing regression/calibration and recall docs.

## Deliverables

- Metamemory answer gate model.
- Answer rendering rules.
- Abstention/clarification/source-audit/probe decisions.
- Trace integration.
- Calibration feedback into answer decisions.

## Acceptance Criteria

- Source-poor answers are blocked or warning-rendered.
- Ambiguous context can trigger clarification.
- Contested claims trigger warning, review, or abstention.
- High-risk procedures require validation.
- Answer gate decision is visible in trace/UI.

## Tests To Add Later

- answer/warn/clarify/source-audit/probe/abstain paths,
- contested claim path,
- redaction-limited answer path,
- high-risk procedure path,
- calibration-risk path.
