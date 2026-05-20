# SB08 Semantic Invariants

## Invariant SB08-EVENT-MASTERY-01

- Invariant ID: `SB08-EVENT-MASTERY-01`
- Source raw note: Professor assimilation must be backed by durable mastery/use/integration events, not keywords or recall source-map mentions.
- Expected behavior: Automatic scan does not assimilate or fade a professor anchor merely because synthesized recall source maps mention the derived memory; accepted-use signal events are required.
- Disallowed shallow implementation: Counting persisted recall synthesis source maps, `internalized`, `mastered`, or similar keywords as mastery proof.
- Failing-first test: `SemanticInvariant_ProfessorAnchorScanRequiresAcceptedUseEventsInsteadOfSourceMapMentions` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt` and `bundle://proof/SB08/transcripts/failing-first-current.txt`.
- Passing test: `bundle://proof/SB08/transcripts/passing-semantic-tests.txt` and `bundle://proof/SB08/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Signals/CognitiveMemorySignalContracts.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`.
- Production assertions: `bundle://proof/SB08/transcripts/source-assertions.txt` proves `ProfessorAnchorAcceptedUse` signals, `CountAcceptedUseEventsAsync`, and the source-map-only negative fixture are present.
- Red-team negative case: Source-map-only repeated use must leave the anchor active with no assimilated memory and no retired timestamp.
- Downstream dependency check: SB09 recall must use assimilated/faded anchors only after accepted-use events exist.

## Invariant SB08-INTEGRATION-02

- Invariant ID: `SB08-INTEGRATION-02`
- Source raw note: Assimilation must require aggregate-ready dream or cluster integration, not any cluster membership.
- Expected behavior: Automatic scan passes only when accepted-use events, independent non-descendant support, and approved/applied aggregate or aggregate-ready cluster integration are all present.
- Disallowed shallow implementation: Treating any cluster membership or same underlying source/evidence pair as independent integration proof.
- Failing-first test: `SemanticInvariant_ProfessorAnchorScanRequiresAcceptedUseEventsInsteadOfSourceMapMentions` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt` proves the previous scan accepted source-map-only evidence too broadly.
- Passing test: `ProfessorAnchor_ScanAssimilatesAndFadesIntegratedMasteryEvidence` and `ProfessorAnchor_ScanRequiresAggregateReadyIntegration` in `bundle://proof/SB08/transcripts/passing-semantic-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs` and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`.
- Production assertions: `bundle://proof/SB08/transcripts/source-assertions.txt` proves integration now checks approved/applied aggregate candidates with at least two source maps or clusters that are `AggregateEligible` and `AggregateReady`; fallback independent support uses `Math.Max` so source and evidence links from the same derived memory are not counted twice.
- Red-team negative case: Accepted-use events plus ordinary `NeedsMoreEvidence` cluster membership must not assimilate the anchor.
- Downstream dependency check: SB09 can treat faded anchors as intentionally integrated only when aggregate-ready proof exists.

## Invariant SB08-TRANSITION-AUDIT-03

- Invariant ID: `SB08-TRANSITION-AUDIT-03`
- Source raw note: Active, Comparing, Assimilated, Faded, Rejected, and returned-to-Active transitions must be closed and auditable.
- Expected behavior: Assimilation/fading writes durable lifecycle transition signals; manual assimilation requires explicit review confirmation; rejected dream comparisons return the anchor to Active with audit signals.
- Disallowed shallow implementation: Mutating `AnchorState` directly without durable transition events or leaving Comparing anchors stuck after rejected validation.
- Failing-first test: The SB08 failing-first baseline in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt` showed scan could fade without accepted events; missing transition audit was repaired in SB08 closure tests.
- Passing test: `ProfessorAnchor_ManualAssimilationRequiresReviewConfirmation`, `ProfessorAnchor_RejectedComparisonReturnsAnchorToActiveWithAudit`, and professor lifecycle regressions in `bundle://proof/SB08/transcripts/passing-semantic-tests.txt` and `bundle://proof/SB08/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorTransitionAudit.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Signals/CognitiveMemorySignalContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs`.
- Production assertions: `bundle://proof/SB08/transcripts/source-assertions.txt` proves `ProfessorAnchorLifecycleTransition` signals, `ProfessorAnchorLifecycle` source kind, review-confirmed manual assimilation gate, service transition audits, and dream rejection return-to-active logic are present.
- Red-team negative case: A rejected aggregate candidate that temporarily compares an active professor anchor must produce `Active -> Comparing` and `Comparing -> Active` signals and leave the persisted anchor Active.
- Downstream dependency check: SB09 can trust anchor states because transitions are now event-audited instead of silent mutations.
