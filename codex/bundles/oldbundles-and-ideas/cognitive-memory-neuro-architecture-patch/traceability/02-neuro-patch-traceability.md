# 02 Neuro Patch Traceability

| Requirement | Architecture files | Contract sketches | Subbundle | Validation focus |
|---|---|---|---|---|
| FR-039 Cognitive Workspace | 17, 18 | `CognitiveWorkspaceFrame`, `WorkingMemorySlot` | 15 | workspace lifecycle, focus/inhibition tests |
| FR-040 Attention Router | 17, 18 | `IAttentionRouter`, `AttentionRoutingDecision` | 15 | explainable route decisions |
| FR-041 Claim/Evidence/Belief Ledger | 20 | `MemoryClaim`, `MemoryBeliefState`, `IClaimEvidenceLedger` | 14 | claim support/attack, contradiction tests |
| FR-042 Evidence Anchors | 20, 21 | `MemoryEvidenceAnchor` | 14 | source span/quote hash/redaction tests |
| FR-043 Memory Mutation Authority | 20 | `IMemoryMutationAuthority`, `MemoryMutationCommand` | 14 | idempotency, review, audit tests |
| FR-044 Schema/Entity/Context Binding | 21 | `IEntityContextBindingService`, `ContextFrameRecord`, `EntityRegistryRecord` | 14, 15 | context boundary tests |
| FR-045 Prediction Errors | 19, 22 | `PredictionExpectation`, `PredictionErrorRecord` | 16 | expected-vs-observed tests |
| FR-046 Salience Signal Ledger | 19 | `CognitiveSignalRecord`, `ICognitiveSignalLedger` | 16 | signal vector and policy tests |
| FR-047 Temporal Episodic Memory | 22 | `TemporalEpisodeRecord`, `EpisodeStepRecord` | 17 | sequence/causality tests |
| FR-048 Replay Scheduler | 22 | `IReplayScheduler`, `MemoryReplayJobRecord` | 17 | priority/replay safety tests |
| FR-049 Procedural Skill Memory | 23 | `ProcedureSkillRecord`, `IProcedureSkillMemoryService` | 18 | skill maturity and failure-mode tests |
| FR-050 Simulation Sandbox | 23 | simulation outputs as metadata/hypothesis contracts | 18 | speculation labeling tests |
| FR-051 Metamemory Answer Gate | 24 | `IMetamemoryAnswerGate`, `MetamemoryAnswerGateDecision` | 19 | answer/warn/clarify/abstain tests |
| FR-052 Workspace-Aware Probing | 18, 19, 20, 24 | workspace + signals + mutation candidates | 15, 16, 19 | probe feedback and correction tests |
| NFR-025 No Direct Public Upsert | 20 | mutation authority | 14 | direct mutation rejection tests |
| NFR-026 No Silent Claim Merge | 20, 21 | claim/context records | 14 | context-separated merge prevention |
| NFR-027 No Scalar-Only Salience | 19 | signal vector | 16 | vector preservation tests |
| NFR-028 Explainable Attention | 18 | attention decision | 15 | trace explanation tests |
| NFR-029 Replay Safety | 22 | replay jobs | 17 | replay cannot promote truth |
| NFR-030 Speculation Labeling | 23 | hypothesis metadata | 18 | simulation cannot become active truth |
| NFR-031 Answer Abstention | 24 | answer gate | 19 | abstention/clarification tests |
| NFR-032 Context Boundary Safety | 21, 24 | context frames | 15, 19 | production/test Docker tests |
| NFR-033 Signal Auditability | 19 | signal records | 16 | evidence/actor/version traceability |
