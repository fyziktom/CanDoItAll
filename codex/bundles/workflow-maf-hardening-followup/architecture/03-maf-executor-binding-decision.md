# ADR: MAF executor binding strategy

Status: Accepted

## Context

CanDoItAll workflow definitions are user-authored canonical graphs. A saved workflow can contain arbitrary node ids, executor ids, shapes, routing metadata, and plugin-provided executors that are not known at compile time.

MAF supports function-bound executors through `BindAsExecutor` and also supports source-generated partial executor types. Source-generated executors provide compile-time validation and better Native AOT positioning for static workflows, but they do not naturally model user-authored dynamic workflow graphs without introducing a second generated code pipeline.

## Decision

Keep `BindAsExecutor` as the production adapter boundary for graph-authored CanDoItAll workflow nodes.

Do not introduce source-generated adapter executors in this follow-up. The current compiler must preserve the canonical workflow model, validate graph semantics before persistence/execution, and bind runtime node handlers from the saved workflow definition.

Source-generated executors may be introduced later for stable, code-owned workflow families only when:

- the workflow shape is static enough to justify generated code;
- a benchmark or Native AOT requirement proves the dynamic binding boundary is a material bottleneck;
- generated executors do not replace the canonical persisted workflow model.

## Consequences

- Dynamic graph authoring remains simple and does not require compiling user workflow definitions into assemblies.
- Runtime safety continues to come from typed validation, executor descriptors, approval gates, payload policy, and backend policy rather than compile-time source generation.
- Native AOT and source-generation optimization remain a targeted future option for stable in-code workflows, not a default for user-authored graphs.

## Validation

- `bundle://proof/SB08/source-assertions-risky-invariants.txt` shows the MAF workflow dependency baseline and the current `BindAsExecutor` adapter boundary.
- `bundle://proof/SB08/unit-targeted-regression.txt` proves the MAF package baseline, workflow graph validation, runtime policy validation, and event normalization tests still pass on the dynamic binding boundary.
- `bundle://proof/SB08/final-verifier-red-team.md` records the R10 closure check and residual trigger for future generated executor work.
