# Assumptions And Risks

## Working Assumptions

- The repository snapshot is the current implementation state after Codex claimed completion.
- The previous bundle in `codex/bundles/cognitive-memory-execution-depth-professor-learning-followup` is available to Codex and should be used as implementation history.
- The current goal is to improve the cognitive-memory foundation and its execution process, not to introduce economic governance.
- Deterministic unit/integration tests are preferred over live LLM calls.
- Optional LLM-assisted synthesis may be designed behind interfaces, but deterministic fallbacks and tests are mandatory.

## Critical Path Risks

- Codex may again mark semantic proof as complete by writing convincing prose into `reviews/01-execution-report.md`.
- Candidate-pair clustering may look multi-key while still missing paraphrases and over-merging bridge clusters.
- Dreaming may remain representative-copy extraction if tests only reject diagnostic boilerplate.
- Professor captures may remain immediately active memories instead of temporary learning anchors.
- Recall synthesis may remain concatenation if tests only check hidden references and absence of exact old text.

## Validation Risks

- Existing tests may pass while the domain behavior remains shallow.
- Completed-stage validation currently cannot verify command transcripts, file hashes, or failing-first history.
- Positive-only tests can hide semantic failures.
- Browser/component tests are not sufficient proof for backend cognition behavior.
- A broad `FullyQualifiedName~CognitiveMemory` test run is useful but cannot replace adversarial targeted assertions.

## Reopen Triggers

- Any critical subbundle lacks a failing-first negative test that fails before production changes.
- Any proof command is cited without a transcript artifact and changed-file manifest.
- Any new aggregate claim is copied verbatim from a single source when the mode required synthesis.
- Any active professor anchor is used as ordinary stable recall knowledge before assimilation.
- Any recall statement cannot be mapped to exact aggregate claim/source lineage on demand.
- Any service grows further without extracting the required domain collaborator.
