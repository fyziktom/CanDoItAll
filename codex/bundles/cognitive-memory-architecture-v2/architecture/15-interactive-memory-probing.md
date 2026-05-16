# Interactive Memory Probing Architecture

## Purpose

Interactive Memory Probing is a controlled dialogue mode for testing, improving, and calibrating Cognitive Memory. The user can talk to the memory system like a student, ask arbitrary questions, challenge answers, demand source explanations, correct mistakes, and convert discovered failures into review items, learning proposals, or regression tests.

This is not normal chat and not a simple RAG front-end. It is a memory evaluation and maintenance loop.

## Core Principle

```text
Memory is maintained not only by ingestion and consolidation, but also by interrogation.
```

The system should learn where its memory is weak by being questioned. Human conversation is valuable because it is irregular, associative, and often jumps across topics that static test suites would not cover.

## Non-Goals

- Do not let a conversation directly overwrite authoritative memory.
- Do not treat user corrections as automatically validated source truth.
- Do not run external learning without approval.
- Do not hide low-confidence, stale, contradictory, or source-poor answers behind fluent wording.
- Do not let probing become only a scalar score or a generic chatbot metric. Probe assessment must use the shared score geometry model.

## Relationship To Existing Memory Layers

| Layer | Role in probing |
|---|---|
| Source snapshots | Ground truth evidence for answers and corrections. |
| Canonical memory | Main recalled material being tested. |
| Recall orchestrator | Produces answers, candidates, context packs, and trace evidence. |
| Recall trace | Explains what was used, excluded, redacted, stale, or low-confidence. |
| Human review | Receives correction, merge/split, supersession, and contradiction candidates. |
| Consolidation | Consumes probe evidence later and updates gap/correction/review state. |
| Epistemic Drive | Generates useful probe questions and consumes probe outcomes as gap evidence. |
| Regression harness | Turns important probe failures into repeatable tests. |

## Probe Session Modes

| Mode | Purpose |
|---|---|
| `FreeDialogue` | User asks arbitrary questions and follows associations naturally. |
| `GuidedExam` | System asks selected questions and user observes how memory answers. |
| `GapHunting` | Focus on weak regions from Epistemic Drive. |
| `ContradictionHunt` | Probe topics with unresolved or suspected contradictions. |
| `ContextSeparationDrill` | Test topics that are semantically similar but scope-separated. |
| `ProcedureDrill` | Verify that procedural memory is actionable and source-grounded. |
| `SourceAudit` | Ask where a belief came from and whether it has authoritative sources. |
| `LearningValidation` | Re-test a topic after an approved learning task. |
| `RegressionReplay` | Re-run stored probe failures as tests. |

## Dialogue Loop

```text
start probe session
  -> choose mode and project scope
  -> user or system asks a question
  -> recall orchestrator builds context with trace
  -> answer renderer produces answer + answer-gate confidence projection + source summary
  -> probe assessor classifies quality and uncertainty
  -> user confirms/corrects/challenges/asks why
  -> system creates evidence records and suggested actions
  -> optional review item / learning proposal / regression test
  -> consolidation and Epistemic Drive consume evidence later
```

## Probe Turn Anatomy

Each turn should store:

- user text or system-generated question,
- normalized intent and scope,
- recall request and recall trace id,
- context pack id,
- answer text,
- score evaluation trace, confidence projection, and calibration risk,
- source refs used,
- redaction/access decisions,
- missing source warnings,
- detected contradictions,
- detected context-separation risks,
- user feedback,
- generated findings,
- suggested next probes,
- optional review item ids,
- optional regression test ids.

## Answer Requirements

In probing mode, the answer renderer must support two levels:

1. **Natural answer:** a useful response to the user's question.
2. **Probe explanation:** why the system believes it, which memory records were used, what was excluded, where it is uncertain, and what would need review.

The UI can show the natural answer first and a trace panel beside it. The system should also respond to metacognitive questions:

```text
Why do you think that?
Which source supports this?
Is this current or stale?
Is this only your inference?
Is there a newer or contradictory record?
What are you unsure about?
What would you ask me to clarify?
```

## Probe Question Generation

Question generation should combine deterministic and serendipitous inputs:

| Source | Example |
|---|---|
| Coverage map weak subregion | Docker networking is weak and high-risk. |
| Stale memory | Old deployment guidance has not been used or validated recently. |
| Contradiction pressure | Two procedure records disagree. |
| Active project direction | Plugin isolation requires Docker/permissions knowledge. |
| User interest signal | User repeatedly asks about memory architecture and probing. |
| Recall failure | Prior question returned low confidence or broad generic answers. |
| Context-separation candidates | Production Docker vs test simulation Docker. |
| Random walk | Controlled jumps across graph regions to mimic human associative testing. |

The generator should keep the full vector/evidence model from Epistemic Drive. Randomness is allowed only as a diversity/coverage tool, not as the primary priority model.

## Serendipity With Guardrails

Human probing is valuable because it jumps across topics. The system should intentionally support controlled randomness:

```text
candidate regions
  -> remove inaccessible/restricted regions
  -> weight by knowledge need vector and active directions
  -> add diversity penalty to avoid one-topic loops
  -> reserve small serendipity budget for low-coverage adjacent regions
  -> generate explainable question set
```

The final question queue should show why each question was selected.

## User Correction Lifecycle

A correction creates evidence, not immediate truth.

```text
user correction
  -> correction evidence record
  -> affected memory/source candidates
  -> risk classification
  -> optional review item
  -> optional contradiction/supersession candidate
  -> optional regression test
  -> consolidation evaluates later
  -> human validation required where policy requires it
```

Low-risk preference corrections may be accepted faster. High-risk procedural, deployment, security, financial, legal, medical, compliance, or destructive automation corrections must require review before becoming active memory.

## Probe Outcome Classes

| Outcome | Meaning | Typical follow-up |
|---|---|---|
| `Confirmed` | Answer is accepted and source-grounded. | Increase confidence/coverage through auditable event. |
| `PartiallyCorrect` | Some content correct, missing nuance. | Create correction candidate and regression test. |
| `Incorrect` | User rejects answer. | Review item, gap evidence, calibration penalty. |
| `MissingKnowledge` | System admits it lacks evidence. | Gap record or learning proposal. |
| `Ambiguous` | Multiple interpretations remain. | Ask clarification, split memory contexts. |
| `ContradictionSuspected` | Conflicting records surfaced. | Contradiction review. |
| `WrongScope` | Answer used related but wrong context. | Context-separation relation/test. |
| `TooGeneric` | Answer lacked project-specific memory. | Gap evidence and source request. |
| `Overconfident` | Confidence high but answer rejected. | Calibration ledger update. |

Probe assessment should reference `ProbeAssessment` and `AnswerGate` score evaluation traces. UI may show a compact confidence value, but the backend must preserve the score vector, matched shapes, missing dimensions, and evidence refs.
| `UnsafeOrRedacted` | Policy prevented answer/source exposure. | Access review or safe explanation. |

## Regression Test Generation

A failed or important probe can become a durable memory regression test:

```text
question
expected answer constraints
required memory/source refs
forbidden context leakage
required uncertainty statement
required relation behavior
policy/access constraints
pass/fail evaluator
```

Example:

```text
Question: Which Docker deployment procedure should be used for production?
Required: mention production deployment source or say source is missing.
Forbidden: using test simulation Docker config as authoritative production config.
Required relation: production and test Docker are semantically related but context-separated.
```

## UI: Cognitive Memory Dialogue Workbench

Recommended layout:

```text
left: probe question queue / knowledge regions / mode selector
center: dialogue
right: recall trace, source refs, confidence, gaps, suggested actions
bottom: correction/review/regression controls
```

Important actions:

- Confirm answer.
- Correct answer.
- Ask why/source.
- Mark as missing knowledge.
- Mark wrong scope.
- Create review item.
- Create regression test.
- Request learning proposal.
- Add source reference.
- Re-run probe after consolidation.

## MAF And Workflow Integration

Add workflow executors/tools for:

- `memory.probe.session.start`
- `memory.probe.ask`
- `memory.probe.generateQuestions`
- `memory.probe.feedback`
- `memory.probe.regression.create`
- `memory.probe.regression.run`
- `memory.probe.learning.validate`

MAF may help generate questions or summarize probe reports, but durable probe records and memory mutations remain owned by Cognitive Memory services.

## Security And Privacy

Probe sessions can contain sensitive user corrections and source discussions. Therefore:

- sessions need project/user/workflow access context,
- redaction policy must run before answers leave the boundary,
- secret-like content must not be embedded or sent to external providers,
- probe transcripts should have retention and export policy,
- cross-project probing must never expose project-private source text without approval,
- user corrections must be classified by risk before promotion.

## MVP Scope

MVP should implement:

1. Probe sessions and turns.
2. Manual user questions.
3. Recall-backed answers with trace ids.
4. Basic feedback: confirm, correct, missing, wrong scope, needs source.
5. Correction evidence records.
6. Review item creation.
7. Regression test creation from a failed turn.
8. Epistemic Drive evidence ingestion from probe outcomes.
9. UI with answer, trace, source refs, confidence, and actions.
10. Docker context-separation probe fixture.

## Future Enhancements

- Adaptive difficulty.
- Spaced repetition for stale but important knowledge.
- Voice conversation mode.
- Team knowledge steward assignments.
- Agent-vs-agent probing.
- Automatic question bank balancing across project regions.
- Calibration dashboards for confidence vs correctness.
- Cross-project reusable probe packs.

## Neuro-Cognitive Probing Updates

Every probe session must attach to or create a cognitive workspace frame. Probe turns update focus slots, open questions, inhibited candidates, and answer-gate decisions.

Important probe turns should record:

- prediction expectation before answer where risk or ambiguity warrants it,
- prediction error after feedback when observed outcome differs from expected behavior,
- cognitive signals such as user interest, wrong scope, source weakness, overconfidence, usefulness, or rework cost,
- selected claim ids and evidence anchors,
- claim-level correction candidates submitted through mutation authority,
- context-frame expectations and violations,
- answer-gate decision and warnings.

Probe corrections must become claim operations such as propose, attack, narrow scope, supersede, or request source anchor. The correction itself does not update active truth.

The Dialogue Workbench should show workspace focus, inhibited candidates, selected claims, evidence anchors, prediction errors, signal records, and answer-gate decision where available.
