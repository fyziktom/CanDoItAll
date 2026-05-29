# 08-final-regression-ci-and-evidence-cleanup

## Objective

Close the follow-up with a reproducible regression matrix and concise evidence.

## Implementation steps

1. Run targeted tests introduced in SB01-SB07.
2. Run broader workflow/plugin/component integration tests.
3. Run solution build.
4. Check for CI status or add/update a minimal workflow if repo policy expects it.
5. Update docs:
   - `docs/workflow-maf-hardening.md`,
   - previous bundle residual risk notes,
   - this bundle execution report.
6. Trim proof:
   - keep command, outcome, and relevant failure snippets,
   - avoid huge source scans,
   - avoid raw secret-looking values,
   - keep screenshots only when UI changed.
7. Add final architecture review:
   - what is fixed,
   - what remains intentionally deferred,
   - production readiness statement.

## Required regression scenarios

- Simple Start -> Executor -> End workflow.
- LLM component workflow with strict JSON settings.
- Predicate route that skips HumanInput.
- Route that reaches HumanInput and waits.
- Approval-required executor denied.
- Approval-required executor approved.
- Oversized executor output artifact split.
- Plugin observer logs a plugin executor failure.
- Durable backend unavailable/runnable distinction.
- Preview simulation avoids live plugin calls.

## Acceptance checklist

- All subbundle gates are reflected in `reviews/01-execution-report.md`.
- Final architecture review is honest.
- Evidence is concise and reproducible.
- Known residual risks have owners and next steps.

## Proof required

- Final build transcript.
- Targeted unit/component/integration test transcript.
- Source assertion transcript for risky invariants.
- Final architecture review.
