# Implementation Prompt For Codex

You are implementing a follow-up bundle for the CanDoItAll cognitive-memory module. Do not optimize for making the current tests pass. Optimize for the semantic invariants in this bundle.

Rules:

1. Read the whole bundle before making code changes.
2. Install and use SB01 skill/validator improvements before continuing.
3. Create failing-first tests in SB02 before production changes.
4. For each critical subbundle, create `proof/SBxx/semantic-invariants.*` and map every implementation change to invariant IDs.
5. Do not mark a subbundle complete unless targeted failing-first and targeted passing transcripts exist and are cited in the proof manifest.
6. Do not hard-code fixture names, test names, or specific example phrases in production logic.
7. Keep deterministic unit tests. Optional LLM/embedding providers must be behind interfaces and disabled in deterministic tests.
8. Do not implement economic governance or attention-budget economics.
9. After each subbundle, reopen changed files and verify the code still satisfies upstream and downstream invariants.
10. If you cannot satisfy an invariant, mark the subbundle blocked and explain the precise blocker instead of downgrading the requirement.
