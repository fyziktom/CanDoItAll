# Executed Refactor Map

## Implemented Splits

| Area | New Boundary | Reason |
| --- | --- | --- |
| External file ingestion | `CognitiveMemoryExternalSourceTextExtractor` | Keeps Office/PDF/text extraction out of operational settings and makes format support testable. |
| Staged source safety | `CognitiveMemoryStagedSourceManifest` | Gives LB4U-style staged ingestion a typed manifest, path containment validation, and explicit exclusions. |
| Model execution settings | `CognitiveMemoryModelExecutionProfile` and related typed contracts | Removes stringly model-role settings and exposes output token budgets for OpenAI/Ollama validation. |
| Consolidation fact extraction | `CognitiveMemoryConsolidationFactExtractor` | Separates planning-dimension/fact extraction from consolidation orchestration. |
| Probe summaries | Redaction and source-aware summary helpers in probe service | Persists useful probe evidence while redacting contact values. |

## Large Files Left Intact

The audit identified larger future split candidates in recall, advanced services, review UI, Blazor page, settings, consolidation, and API route mapping. This execution did not split every large file because the highest-risk gaps were behavioral: realistic source extraction, provenance, review quality, model profiles, epistemic proposals, and probes. The implemented splits isolate the new stable responsibilities without broad route/UI churn.

## Validation

- Unit Cognitive Memory tests: 113/113 passed.
- Integration Cognitive Memory tests: 25/25 passed.
- Component Cognitive Memory tests: 1/1 passed.
- Serial solution build passed with existing `Google.Protobuf` warnings.
