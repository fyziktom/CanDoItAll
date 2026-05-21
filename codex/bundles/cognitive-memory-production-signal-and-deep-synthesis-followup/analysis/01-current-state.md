# Current State Review

## What is genuinely improved

- The bundle workflow skill now documents artifact-backed proof, portable references, semantic invariants, transcripts, changed-file hashes, failing-first and passing evidence, source assertions, and anti-stub audits.
- The prepared/completed validator can resolve `repo://` and `bundle://` references, checks proof manifests for critical completed subbundles, validates SHA-256 hashes and transcript exit codes, and rejects missing critical proof files.
- Static production use of `CognitiveMemoryQualityAlgorithmOptions.Current` appears to be removed from production source. Current direct references are the options class itself and tests.
- Cross-project clustering is no longer blocked by the old project-only filter for cross-project modes.
- Professor anchors now have states, direct professor anchors are hidden from default recall, and direct capture memories are not accepted as assimilation proof.
- Dream validation has more checks than before: independence, active professor anchors, duplicate aggregate detection, source-map checks, stale/superseded checks, restricted content, and confidence calibration.
- Recall synthesis hides references by default and can resolve aggregate references on demand.

## What is still insufficient

- The workflow validator proves evidence shape, not production semantic adequacy. It did not catch that `ProfessorAnchorAcceptedUse` is only an enum/evaluator/test-seeded signal and has no production emitter.
- Professor assimilation can be blocked forever in real operation because accepted-use evidence is required but not emitted by recall/workflow outcome paths.
- `ScanAssimilationAsync` exists but is not wired into scheduled automation or post-dream/post-recall lifecycle flows.
- Dream synthesis still creates meta-statements like `Conclusion: X is supported by N source-backed observation(s)` and joins source claim text. This is not yet the desired internalized memory.
- Source maps are still too coarse: each claim unit receives the record-wide source maps, not evidence that is specific to the exact claim being synthesized.
- Professor capture remains English-keyword heavy, with only limited ASCII Czech phrases in curator capture kind detection. Czech diacritics and natural Q&A teaching can be missed.
- Recall synthesis still uses the context pack title/summary as the query in `CognitiveMemoryRecallSynthesisService`, not the real user query or requested task intent.
- Persisted statement source maps still risk broad Cartesian attachment of source references to aggregate claims.
- Approximate clustering is lexical/signal-based and can miss paraphrases without shared extracted keys. It should use the existing embedding/ranker/provider layer when available.
- Several cognitive memory services remain very large, which makes future Codex passes prone to simplifying behavior inside huge methods instead of preserving domain boundaries.

## Most important proof failure found

The completed SB08 proof in the previous bundle claims event-backed professor assimilation. The current production source only contains:

- an enum value for `ProfessorAnchorAcceptedUse`,
- evaluator logic that counts that signal,
- tests that manually seed the signal.

There is no production emitter that records accepted use after a recall brief or workflow outcome is accepted. This is exactly the kind of shallow-pass trap the workflow skill must prevent.
