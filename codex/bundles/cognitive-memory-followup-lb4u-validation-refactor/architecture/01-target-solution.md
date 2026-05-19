# Target Solution

## Target Behavior

Cognitive memory should behave as a staged, provenance-first learning system:

- External source ingestion produces raw source items, typed chunks, and asset references.
- Consolidation proposes canonical memories with source support, category, confidence, and review state.
- Recall builds a context pack from lexical, vector, graph, temporal, and score channels and explains source coverage.
- Probing uses recall, answer generation where configured, feedback, and review decisions without directly mutating truth.
- Epistemic drive scans coverage and gaps to propose deeper study and reusable cross-project knowledge.
- Model provider settings are explicit per memory role, with model id and output token policy visible in evidence.

## Proposed Runtime Flow

1. Build a stage manifest for LB4U.
2. Validate exclusions and source file readability.
3. Extract typed chunks from each stage.
4. Persist raw source items and asset references.
5. Run consolidation for the stage.
6. Inspect candidates and review decisions.
7. Run probes and record source-backed context.
8. Trigger deeper study when recall gaps appear.
9. Run epistemic-drive scans for reusable planning knowledge.
10. Repeat with OpenAI, then validate with Ollama.

## Model-Assisted Boundaries

- Model-assisted extraction may propose sections, summaries, candidate facts, and reusable patterns.
- Model output must be stored as generated evidence or review candidates, not raw truth.
- Canonical memory changes require existing review/policy paths.
- Model provider failure, truncation, or missing token metadata must produce visible failure state.
- Deterministic heuristics remain useful as checks and fallback-disabled tests, but they cannot masquerade as successful model-assisted validation.

## Data Shape Additions To Consider

- `CognitiveMemoryStageManifest`
- `CognitiveMemoryStageId`
- `CognitiveMemorySourceExclusion`
- `CognitiveMemorySourceChunk`
- `CognitiveMemorySourceSpan`
- `CognitiveMemoryModelRole`
- `CognitiveMemoryModelExecutionProfile`
- `CognitiveMemoryTokenBudget`
- `CognitiveMemoryModelExecutionResult`
- `CognitiveMemoryTruncationState`
- `CognitiveMemoryProbeObservation`

Names are illustrative. Execution should prefer existing naming patterns and avoid schema churn unless behavior requires it.

## API Shape Additions To Consider

- Stage manifest validation endpoint or request model on existing ingestion endpoint.
- Source exclusion reporting in ingestion operation status.
- Model execution profile settings under existing cognitive memory settings.
- Token/truncation metadata on consolidation/probe/professor-review responses.
- Probe observation export for regression and workbook evidence.

Any API addition must update integration tests, API docs, and `candoitall-api-cognitive-memory`.
