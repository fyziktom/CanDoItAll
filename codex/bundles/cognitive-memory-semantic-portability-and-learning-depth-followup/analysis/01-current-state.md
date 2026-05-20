# Current State Review

## What Codex actually improved

Codex made real progress compared with the previous version:

- The bundle workflow skill now requires proof manifests, command transcripts, changed-file hashes, source assertions, failing-first evidence, positive evidence, anti-stub audits, and red-team closure for critical subbundles.
- `validate_bundle.py` now performs completed-stage proof checks for critical subbundles, including manifest presence, SHA-256 hashes, transcript existence, exit-code checks, fake-proof fixtures, and cited test-name checks.
- Clustering is no longer purely single-key grouping. `CognitiveMemoryClusterPlanner` now delegates key extraction and candidate selection to collaborators and uses composite edge scoring.
- Bridge-chain overmerge is mitigated by clique-like join rules: a candidate must connect to all current members with sufficient score.
- Dreaming now has claim synthesis and claim-level source maps, and aggregate apply confidence is better calibrated than the old unconditional `StrongAccept` behavior.
- Curator/professor mode has structured professor anchor extraction, anchor states, an assimilation service, and tests for direct-capture rejection and descendant-only support rejection.
- Recall synthesis hides references by default, persists statement source maps, and can resolve references on demand.
- Several responsibilities were extracted into collaborators and registered in DI.

## Remaining process gaps

The completed Codex bundle is still not fully reliable as a portable proof artifact:

- Running completed-stage validation in the reviewed Linux environment failed because many subbundle source references and proof-manifest references use `C:/repositories/...` paths. `validate_exact_source_references` relies on `Path(reference).is_absolute()`, which does not treat Windows paths as absolute on Linux. The artifact validator has a Windows-path regex, but source-reference validation does not use it.
- Proof manifests are machine-path dependent. A bundle that passed on one Windows machine may fail after being archived, moved, or checked by a Linux/WSL/CI agent.
- The validator verifies artifact shape, transcripts, hashes, and labels, but it still does not verify that each raw requirement has a behavior-level invariant tied to production code, failing-first test, passing test, and red-team negative case.
- The anti-stub audit searches for obvious markers but cannot detect narrow hard-coded fixture behavior or implementations that satisfy only the added test names.

## Remaining cognitive-memory gaps

- Cross-project weekly dreaming is not truly cross-project. `CognitiveMemoryClusterPlanner` filters by `ProjectId`, and `CognitiveMemoryCandidatePairSelector.AddPair` rejects pairs where `left.Record.ProjectId != right.Record.ProjectId`. `CrossProjectWeekly` can therefore become same-project dreaming with a cross-project label.
- Approximate semantic candidate discovery is still too limited. Candidate pairs are preselected by exact strong keys. The fallback only runs for over-fanout exact-key groups, so paraphrases without exact shared keys may never be compared.
- Cluster key coverage is not stored. `BuildSharedClusterKeys` accepts a key shared by any two members, even in larger clusters, so a cluster can expose a key that does not represent most members.
- Dream claim grouping is too coarse. `BuildClaimSignature` currently uses only mode plus primary cluster key, ignoring the claim text and semantic claim slots. Unrelated claims in the same cluster can be forced into a single synthesized claim group.
- Dream synthesis is still mostly string concatenation/common-prefix merging. It can join statements without producing a disciplined abstraction with slots, conditions, caveats, and support roles.
- Entailment validation is lexical. It catches a narrow approval-bypass pattern but will miss many negation, numeric, temporal, conditional, actor/action, and optional/required reversals.
- Professor extraction remains keyword-driven and rejects many natural professor conversations: short corrections, question-answer teaching, examples/counterexamples, and explicit capture scenarios.
- All curator captures are currently marked with `AnchorState = Active`, even when they are not professor anchors. This blurs lifecycle semantics.
- Assimilation mastery is still inferred from text keywords such as `internalized`, `mastered`, `repeated use`, or `reinforced`. This is not a durable proof that the memory has actually been internalized.
- Repeated-use counting currently counts persisted recall synthesis source-map usage, not successful consumer feedback or correct reuse.
- Dream/cluster integration is too easy to satisfy; any cluster membership can count, even if the cluster is weak or not aggregate-ready.
- Recall brief composition is improved but still heuristic: it extracts first useful lines and joins fragments. It does not yet have a typed answer plan, caveat plan, audience/context adaptation, or fully precise statement-to-claim lineage.

## Maintainability state

- `CognitiveMemoryCuratorConversationService.cs` remains over 1200 lines.
- `CognitiveMemoryClusterPlanner.cs` remains close to 900 lines.
- `CognitiveMemoryDreamConsolidationService.cs` remains close to 800 lines.
- Several services still use static `CognitiveMemoryQualityAlgorithmOptions.Current` access, which makes environment-specific tuning and test isolation harder.
- Extracted collaborators help, but domain responsibilities are still partly mixed across persistence, orchestration, heuristics, scoring, formatting, and lifecycle transitions.
