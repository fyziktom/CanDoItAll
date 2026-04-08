# Example prompts

## Deep canonical-model review

Use $canonical-model-review on this repository. Treat the system as a project operating system with agents, mindmap nodes, relations, projections, snapshots, and runtime tooling. Review the canonical model, identify source-of-truth boundaries, and produce a stabilization plan.

## Review after a new feature block

Use $feature-block-architecture-review on the recently added CRM/HR block. Classify the new and changed types into domain core, policy, workflow, projection, integration, UI state, or runtime tooling. Highlight what must be stabilized before the next feature wave.

## Periodic drift audit

Use $architecture-drift-audit. Compare the current state of the architecture against the previous review reports if they exist under architecture/reviews. Focus on new drift, overloaded abstractions, projection leakage, and layering erosion.

## Review with subagents

Use $canonical-model-review. Spawn `arch_mapper` to map the solution, `canonical_model_skeptic` to challenge source-of-truth boundaries, and `runtime_validator` to run the smallest safe build/test validation. Then synthesize the final report.
