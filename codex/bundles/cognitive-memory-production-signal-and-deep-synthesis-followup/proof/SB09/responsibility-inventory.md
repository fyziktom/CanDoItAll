# SB09 Responsibility Inventory

## New Boundaries

| Responsibility | Old placement | New boundary | Reason |
|---|---|---|---|
| Accepted-use production signal emission | No production producer existed; tests seeded signal rows | `ICognitiveMemoryProfessorAcceptedUseSignalEmitter` and `CognitiveMemoryProfessorAcceptedUseSignalEmitter` | Isolates validation, lineage checks, signal publication, and post-publication assimilation scan. |
| Approximate cluster candidate generation | `CognitiveMemoryCandidatePairSelector` owned exact, fanout, fallback, and semantic approximate pairing | `ICognitiveMemoryApproximateClusterCandidateProvider` and `CognitiveMemoryEmbeddingBackedApproximateClusterCandidateProvider` | Separates approximate candidate policy from exact preselection and makes continuation/metrics explicit. |
| Claim-specific dream provenance | Aggregate candidate creation flattened source maps inline | `CreateClaimSpecificSourceMaps` and `ClaimSourceMapSupportsUnit` | Keeps provenance scoping readable and testable without moving unrelated dream-run orchestration. |
| Recall synthesis query resolution | Inline title/summary composition inside synthesis | `ResolveSynthesisQueryText` helper plus request `QueryText`/`Intent` | Makes task query precedence explicit while preserving compatibility. |
| Professor comparison resolution | No explicit command surface | `ResolveComparisonAsync` with typed outcome request/result | Keeps human review lifecycle transitions out of ad hoc state mutation. |

## Risk Decision

No broad file-splitting refactor was attempted after the behavior fixes. The large curator and dream services are still candidates for later extraction, but this bundle’s production risk was in missing producers/lifecycle paths and shallow provenance. The implemented boundaries reduce those responsibilities without destabilizing unrelated flows.

## Validation Targets

- Focused production-signal, lifecycle, multilingual capture, dream provenance, approximate provider, and recall-lineage tests: 9 passed.
- Affected Cognitive Memory class set: 119 passed.
- Module registration: validates the new approximate provider and accepted-use emitter registration descriptor.
