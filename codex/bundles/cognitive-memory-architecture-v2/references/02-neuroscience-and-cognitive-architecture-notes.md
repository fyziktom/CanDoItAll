# Neuroscience And Cognitive Architecture Notes

## Use Neuroscience Conservatively

This patch uses neuroscience as engineering inspiration, not as a claim that CanDoItAll is conscious, sentient, or biologically equivalent to the brain.

## Relevant Principles

### Working Memory And Executive Control

Working memory is not just stored short-term text. It involves controlled attention, integration of information, and temporary binding of multiple sources into a current cognitive workspace.

Software mapping:

- cognitive workspace frame,
- focus slots,
- goal stack,
- context budget,
- inhibition of related-but-wrong candidates.

### Hippocampal Replay And Consolidation

Replay/reactivation literature motivates scheduled consolidation, rehearsal, and planning-like re-evaluation of recent or important episodes.

Software mapping:

- replay scheduler,
- episode consolidation,
- regression replay,
- source re-anchoring,
- procedure validation.

### Predictive Coding / Prediction Error

Predictive-processing and active-inference ideas motivate storing expected-vs-observed mismatches as learning signals.

Software mapping:

- prediction expectation,
- prediction error record,
- surprise/salience signal,
- answer gate and probing decisions.

### Salience And Neuromodulatory Analogy

Human cognition gives extra weight to surprising, risky, rewarding, repeated, or emotionally significant information. For enterprise software, this should be translated into auditable salience signals, not opaque emotion simulation.

Software mapping:

- novelty,
- risk,
- usefulness,
- rework cost,
- user interest,
- contradiction pressure,
- calibration risk.

### Metamemory

Metamemory is awareness of what is known, uncertain, or unreliable. In software, this maps to answer gating, source sufficiency, abstention, and confidence calibration.

Software mapping:

- answer gate,
- abstention,
- source audit request,
- clarification routing,
- probing recommendation.

## References For Human Review

- Squire et al., memory consolidation and hippocampal/cortical reorganization, PMC: `https://pmc.ncbi.nlm.nih.gov/articles/PMC4526749/`
- Ólafsdóttir et al., hippocampal replay in memory and planning, PMC: `https://pmc.ncbi.nlm.nih.gov/articles/PMC5847173/`
- Baddeley, working memory models and episodic buffer, PMC: `https://pmc.ncbi.nlm.nih.gov/articles/PMC11979773/`
- Brown et al., active inference, attention, and motor preparation, PMC: `https://pmc.ncbi.nlm.nih.gov/articles/PMC3177296/`
- Limanowski, precision in active inference, PMC: `https://pmc.ncbi.nlm.nih.gov/articles/PMC11431491/`

## Architecture Rule

Biology inspires responsibilities, not implementation details. The system remains:

- source-grounded,
- auditable,
- policy-controlled,
- reviewable,
- deterministic where possible,
- honest about uncertainty.
