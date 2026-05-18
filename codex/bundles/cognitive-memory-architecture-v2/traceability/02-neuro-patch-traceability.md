# 02 Neuro Patch Traceability

| Requirement | Architecture files | Contract sketches | Subbundle | Validation focus |
|---|---|---|---|---|
| FR-039 Cognitive Workspace | 17, 18 | `CognitiveWorkspaceFrame`, `WorkingMemorySlot` | `15-cognitive-workspace-attention-router` | workspace lifecycle, focus/inhibition tests |
| FR-040 Attention Router | 17, 18 | `IAttentionRouter`, `AttentionRoutingDecision` | `15-cognitive-workspace-attention-router` | explainable route decisions |
| FR-041 Claim/Evidence/Belief Ledger | 20 | `MemoryClaim`, `MemoryBeliefState`, `IClaimEvidenceLedger` | `14-neuro-foundation-claim-evidence-ledger` | claim support/attack, contradiction tests |
| FR-042 Evidence Anchors | 20, 21 | `MemoryEvidenceAnchor` | `14-neuro-foundation-claim-evidence-ledger` | source span/quote hash/redaction tests |
| FR-043 Memory Mutation Authority | 20 | `IMemoryMutationAuthority`, `MemoryMutationCommand` | `14-neuro-foundation-claim-evidence-ledger` | idempotency, review, audit tests |
| FR-044 Schema/Entity/Context Binding | 21 | `IEntityContextBindingService`, `ContextFrameRecord`, `EntityRegistryRecord` | `14-neuro-foundation-claim-evidence-ledger` + `15-cognitive-workspace-attention-router` | context boundary tests |
| FR-045 Prediction Errors | 19, 22 | `PredictionExpectation`, `PredictionErrorRecord` | `16-prediction-error-salience-signals` | expected-vs-observed tests |
| FR-046 Salience Signal Ledger | 19 | `CognitiveSignalRecord`, `ICognitiveSignalLedger` | `16-prediction-error-salience-signals` | signal vector and policy tests |
| FR-047 Temporal Episodic Memory | 22 | `TemporalEpisodeRecord`, `EpisodeStepRecord` | `17-temporal-replay-scheduler` | sequence/causality tests |
| FR-048 Replay Scheduler | 22 | `IReplayScheduler`, `MemoryReplayJobRecord` | `17-temporal-replay-scheduler` | priority/replay safety tests |
| FR-049 Procedural Skill Memory | 23 | `ProcedureSkillRecord`, `IProcedureSkillMemoryService` | `18-procedural-skill-memory-simulation` | skill maturity and failure-mode tests |
| FR-050 Simulation Sandbox | 23 | simulation outputs as metadata/hypothesis contracts | `18-procedural-skill-memory-simulation` | speculation labeling tests |
| FR-051 Metamemory Answer Gate | 24 | `IMetamemoryAnswerGate`, `MetamemoryAnswerGateDecision` | `19-metamemory-abstention-calibration` | answer/warn/clarify/abstain tests |
| FR-052 Workspace-Aware Probing | 18, 19, 20, 24 | workspace + signals + mutation candidates | `15-cognitive-workspace-attention-router` + `16-prediction-error-salience-signals` + `19-metamemory-abstention-calibration` | probe feedback and correction tests |
| NFR-025 No Direct Public Upsert | 20 | mutation authority | `14-neuro-foundation-claim-evidence-ledger` | direct mutation rejection tests |
| NFR-026 No Silent Claim Merge | 20, 21 | claim/context records | `14-neuro-foundation-claim-evidence-ledger` | context-separated merge prevention |
| NFR-027 No Scalar-Only Salience | 19 | signal vector | `16-prediction-error-salience-signals` | vector preservation tests |
| NFR-028 Explainable Attention | 18 | attention decision | `15-cognitive-workspace-attention-router` | trace explanation tests |
| NFR-029 Replay Safety | 22 | replay jobs | `17-temporal-replay-scheduler` | replay cannot promote truth |
| NFR-030 Speculation Labeling | 23 | hypothesis metadata | `18-procedural-skill-memory-simulation` | simulation cannot become active truth |
| NFR-031 Answer Abstention | 24 | answer gate | `19-metamemory-abstention-calibration` | abstention/clarification tests |
| NFR-032 Context Boundary Safety | 21, 24 | context frames | `15-cognitive-workspace-attention-router` + `19-metamemory-abstention-calibration` | production/test Docker tests |
| NFR-033 Signal Auditability | 19 | signal records | `16-prediction-error-salience-signals` | evidence/actor/version traceability |
