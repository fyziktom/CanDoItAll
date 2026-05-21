# Implementation Prompt

You are executing the Cognitive Memory Production Signal And Deep Synthesis follow-up bundle. Do not start production feature work until SB01 installs stronger workflow/validator gates and SB02 proves the current implementation fails the new behavioral regressions.

For every new domain signal, state, record, or lifecycle transition, prove all of the following:

- producer path in production code,
- consumer path in production code,
- lifecycle/scheduler/review path where relevant,
- failing-first test that fails without the production behavior,
- passing test that does not seed the production-only signal directly,
- source assertions that distinguish producer from consumer,
- red-team negative case proving shallow implementations fail.

Do not treat manually seeded database rows as proof of production behavior. Do not close any subbundle by saying the remaining gap is residual risk if it is required for the behavior to work.
