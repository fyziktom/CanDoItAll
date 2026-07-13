# QA Prompt

Use this prompt for subbundle validation and final closure.

```text
Review the selected subbundle for repo://codex/bundles/candoitall-process-maf-hardening-implementation.

Gate checks:
- Every owned requirement has implementation proof or an explicit blocker.
- Failing-first proof would fail the old fragile behavior.
- Passing proof exercises production code, not only fixtures or table existence.
- Negative tests reject shallow implementations.
- All critical proof paths in proof/SBxx/manifest.md exist.
- Semantic invariants name source raw notes, expected behavior, disallowed shallow behavior, failing/passing transcripts, source assertions, and downstream dependency checks.
- Production behavior artifact matrix exists when new production records/signals/states/events are introduced.
- Architecture guard proof shows dependency direction, no fake separation, testability, and partial-class policy compliance.
- Operator-visible changes are validated through projection/host tests and browser proof if UI rendering changed.

Reject closure when:
- Child folder existence is accepted as handoff proof.
- A missing required tool starts an LLM run before deterministic preflight.
- Operator action still recommends blind retry.
- Artifact ledger uses original command result after finalization changed the outcome.
- Template prose still carries a hard gate that runtime cannot validate.
- Tests instantiate the old large class as the only way to exercise extracted behavior.
```
