# Handoff Rules

## Preparation To Execution

Preparation must hand off:

- saved raw inputs
- normalized requirements
- explicit plan
- dependency map
- critical foundation list
- numbered subbundles
- proof rules
- proof tier per subbundle
- progression gates
- readiness-validator result
- self-review

Execution must preserve and update:

- subbundle intent
- traceability
- proof artifacts
- gate results
- execution status
- residual risks
- reopened-phase decisions

## Resume Handoff

After compaction, interruption, or transfer to another agent, the workflow must be recoverable from bundle files. The minimum durable state is:

- bundle root and current subbundle
- raw inputs owned by the active subbundle
- latest entry or closure gate decision
- proof already captured and proof still missing
- browser or host validation analytics state
- blockers, reopened phases, and downstream dependencies that must be rechecked

## Compatibility Rule

Do not invent a second bundle shape during execution. If the bundle needs improvement, improve the existing structure instead of migrating into a different format mid-task.

When the shape is non-canonical, the root compatibility map must identify the files that own inputs, requirements, current state, dependency plan, work units, proof/status, and closure. Structural validation may be recorded as not applicable when the manual semantic gates pass.
