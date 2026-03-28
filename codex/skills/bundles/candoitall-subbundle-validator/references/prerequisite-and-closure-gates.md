# Prerequisite And Closure Gates

## Entry Gate

Confirm all of these before implementation starts:

- the current subbundle still matches the raw inputs it owns
- every listed prerequisite is complete or honestly blocked
- prerequisite proof is still trusted after the latest repo observations
- exact source references still exist and still point at the right surfaces
- the dependency map still matches the intended execution order

## Closure Gate

Confirm all of these before the next subbundle starts:

- acceptance checklist items are finished
- proof-required items are finished
- browser or host proof was captured when required
- screenshot review questions were answered while the screenshot was visible
- the subbundle gate row and browser analytics row were updated
- if the subbundle is a critical foundation, one dependent-flow smoke or downstream surface check passed
