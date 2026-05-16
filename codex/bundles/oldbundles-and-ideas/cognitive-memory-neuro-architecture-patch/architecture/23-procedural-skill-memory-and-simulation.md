# 23 Procedural Skill Memory And Simulation Sandbox

## Purpose

Upgrade procedural memory from passive runbooks to validated skill records that can safely support workflows and agents.

Human procedural memory is not just declarative knowledge about a procedure. It is the ability to perform a sequence under conditions, recover from mistakes, and improve through feedback. The software equivalent is a typed, validated, replayable procedure graph.

## Procedure Skill Record

A procedure skill should include:

- skill id,
- project/global scope,
- title and purpose,
- context frame ids,
- required roles/tools/plugins,
- preconditions,
- input schema,
- step graph,
- postconditions,
- output schema,
- failure modes,
- rollback/compensation steps,
- validation evidence,
- last successful episode id,
- maturity level,
- risk level,
- automation binding,
- source anchors,
- review state.

## Procedure Step

Each step should include:

- step id,
- sequence or graph position,
- action description,
- tool/plugin/workflow executor binding,
- required input,
- expected output,
- validation check,
- timeout/retry policy,
- failure handling,
- source/evidence refs.

## Failure Mode Record

Failure modes should be first-class:

- failure condition,
- detection signal,
- likely cause,
- mitigation,
- rollback/compensation,
- related prediction errors,
- related episodes,
- confidence and validation state.

## Maturity Levels

Suggested levels:

| Level | Meaning |
|---|---|
| `Draft` | Extracted or generated, not validated. |
| `Observed` | Seen in one or more episodes. |
| `Reviewed` | Human/QA reviewed. |
| `Validated` | Passed tests or successful executions. |
| `Automatable` | Safe enough for workflow/tool execution under policy. |
| `Deprecated` | Superseded or unsafe. |

## Relationship To Workflows

Procedural skills can become workflow templates or workflow executor guidance only after validation policy allows it.

A high-risk skill must not become executable automation from generated text alone.

## Simulation Sandbox

The simulation sandbox is for hypothetical planning and analogy:

- test whether a procedure is likely to work,
- compare alternatives,
- generate expected failure modes,
- explore cross-project analogies,
- create review questions.

Simulation outputs are speculative and must be labeled as hypotheses.

## Simulation Output Types

- candidate plan,
- risk analysis,
- missing preconditions,
- expected outcome,
- likely failure modes,
- required sources/tests,
- suggested probe/regression cases,
- procedure improvement proposal.

## Safety Rules

- Simulation output is not source truth.
- A simulated procedure cannot become active without source evidence and validation.
- High-risk automation requires human review.
- Cross-project analogies must respect source access policy.
- The system must distinguish "has worked before" from "might work".

## Required Updates

- Extend procedure extractor contracts to produce `ProcedureSkillRecord`, not only generic memory items.
- Add procedure maturity and validation evidence to UI.
- Add procedure-specific probing and regression tests.
- Add workflow integration rules for procedure-to-template promotion.
