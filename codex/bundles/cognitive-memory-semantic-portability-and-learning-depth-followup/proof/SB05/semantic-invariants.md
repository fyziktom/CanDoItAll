# SB05 Semantic Invariants

## Invariant SB05-CLAIM-GROUPING-01

- Invariant ID: `SB05-CLAIM-GROUPING-01`
- Source raw note: Dream claim grouping must not merge unrelated claims by mode plus primary cluster key only.
- Expected behavior: Claim grouping uses semantic claim signatures or slots so tenant-data export claims and payment-batch export claims stay separate even when they share a primary cluster key.
- Disallowed shallow implementation: Building claim signatures from only dream mode and cluster primary key.
- Failing-first test: `SemanticInvariant_DreamRunSeparatesUnrelatedClaimsSharingPrimaryClusterKey` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`.
- Passing test: `bundle://proof/SB05/transcripts/passing-semantic-tests.txt` and `bundle://proof/SB05/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: `bundle://proof/SB05/transcripts/source-assertions.txt` proves claim slots, slot extraction, claim signatures, representative slots, and structured synthesis markers are present.
- Red-team negative case: A single aggregate claim must not contain both tenant-data export and payment-batch export facts.
- Downstream dependency check: SB06 validation and SB09 recall lineage depend on claim-level separation.

## Invariant SB05-STRUCTURED-SYNTHESIS-02

- Invariant ID: `SB05-STRUCTURED-SYNTHESIS-02`
- Source raw note: Dream synthesis must be structured claim synthesis, not string concatenation.
- Expected behavior: Synthesized claims expose conclusion, support role, condition, and caveat semantics for procedure/failure/coverage modes.
- Disallowed shallow implementation: Joining source fragments with commas, common prefixes, or `and` while losing conditions and caveats.
- Failing-first test: `SemanticInvariant_DreamClaimSynthesisProducesStructuredSlots` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`.
- Passing test: `bundle://proof/SB05/transcripts/passing-semantic-tests.txt` and `bundle://proof/SB05/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: `bundle://proof/SB05/transcripts/source-assertions.txt` proves the syntheses include `Conclusion:`, `Support:`, `Condition:`, and `Caveat:` slots instead of common-prefix/string-join output.
- Red-team negative case: Conditional rollback-owner facts must not be flattened into an unconditional procedure step.
- Downstream dependency check: SB08 assimilation and SB09 recall must be able to cite structured aggregate claim support.
