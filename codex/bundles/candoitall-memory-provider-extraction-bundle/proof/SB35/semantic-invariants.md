# SB35 Semantic Invariants

## Raw request closure

- The repair explicitly owns typed Disabled/Automatic/ExplicitDirective modes.
- One agent may bind and invoke multiple provider instances through stable aliases.
- The mem:<alias> directive selects an explicit provider set without implicit fallback.
- Generic memory stays provider-neutral; native Cognitive Memory remains external and optional.
- Structural repair removes capability-grouping partial classes rather than renaming them.

## Shallow-pass traps

- A policy object containing `DenyImplicitFallback` is insufficient if the registry still dispatches.
- A status call returning a stored record is insufficient unless requester ownership is authorized first.
- Unit contexts with fabricated memory tags are insufficient proof of production identity propagation.
- Multiple registered providers are insufficient proof that one agent can intentionally invoke several.
- A provider manifest or `501` route is insufficient proof of an implemented capability.
- Moving the same large partial classes to another folder/project is not modularization.

## Adversarial negative proof

The SB35 failing-first tests intentionally fail against the current production code for two security/intent invariants:

1. no selected provider exists without an explicit/assigned/default choice under deny fallback;
2. a different requester/agent/session cannot observe or cancel another operation.

SB36 must make both green without changing their semantic assertions to accept current behavior.

## Downstream dependency check

- SB36 establishes the fail-closed single-provider application boundary.
- SB37 may fan out only through explicit immutable provider plans built from typed settings.
- SB38 may transport only the already selected and authorized request with complete context.
- SB39 may trust project context only after authenticated claim matching and domain access policy.
- SB40 cannot close from mock/DTO/source-scan proof alone.

## Gate invariant

SB35 authorizes production repair because its architecture artifacts, semantic characterization failures, and prepared validator pass. It does not assert that any production blocker is already fixed.

## Validator Invariant Contract

- Invariant ID: SB35-ARCH-READY
- Source raw note: repair the poor partial-class architecture and prove one agent can safely choose multiple external memory providers.
- Expected behavior: characterization fails on implicit fallback/foreign ownership before production repair, and target ownership/dependency/testability records gate implementation.
- Disallowed shallow implementation: reuse historical completion, rename partial files, or treat a passing build as architecture proof.
- Failing-first test: bundle://proof/SB36/transcripts/failing-first-evidence.txt.
- Passing test: bundle://proof/SB40/transcripts/terminal-validation.txt and bundle://proof/SB40/transcripts/source-and-architecture-audit.txt.
- Changed source files: this gate changed characterization tests and bundle architecture records; production repair was intentionally absent.
- Production assertions: SB35 itself asserts no shipped behavior; SB36-SB40 own and prove the production corrections.
- Red-team negative case: bundle://proof/SB40/transcripts/red-team-closure.txt rejects historical/focused/mock-only closure.
- Downstream dependency check: SB36-SB40 completed against the recorded boundary and dependency decisions.
