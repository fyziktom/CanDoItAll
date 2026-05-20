# QA Prompt For Codex

Review the implemented subbundle as a skeptical CTO and cognitive-memory QA reviewer.

For every critical subbundle, verify:

- Does the implementation satisfy the semantic invariant, or only the test wording?
- Does the proof work after moving the repo or bundle to a different root path?
- Is there a failing-first transcript and a passing transcript for the same invariant?
- Does the anti-stub audit catch hard-coded fixture behavior, not only TODO/NotImplemented markers?
- Are production source changes necessary and mapped to invariant IDs?
- Are downstream services affected and retested?
- Does the result preserve deterministic testability?
- Does the result avoid economic-governance scope creep?

Reject closure if the answer is unclear.
