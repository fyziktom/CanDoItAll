# Current State

## Original Bundle Contract

The original bundle `cognitive-memory-architecture-v2` is marked complete. Its contract is broader than simple vector search:

- Qdrant is a projection, not source of truth.
- Raw source provenance is mandatory.
- Generated summaries are not raw truth.
- MAF/agent context is executive context, not durable memory.
- Distributed workers cannot directly mutate truth.
- Probing cannot directly mutate truth.
- Public direct upsert semantics are not allowed.
- Cognitive memory must support staged recall, consolidation, epistemic drive, probing, review, score geometry, self-regulation, cross-project promotion, and distributed work.

The original execution report claims unit, integration, component, Playwright, build, and PostgreSQL smoke proof passed. This follow-up treats that as historical evidence, not proof that the current behavior is good enough for realistic staged project memory.

## Current Implementation Surface

Current source inventory shows a broad module:

- `src\CanDoItAll.Modules.CognitiveMemory\Advanced`
- `src\CanDoItAll.Modules.CognitiveMemory\Common`
- `src\CanDoItAll.Modules.CognitiveMemory\Consolidation`
- `src\CanDoItAll.Modules.CognitiveMemory\Foundation`
- `src\CanDoItAll.Modules.CognitiveMemory\Ingestion`
- `src\CanDoItAll.Modules.CognitiveMemory\Neuro`
- `src\CanDoItAll.Modules.CognitiveMemory\Pages`
- `src\CanDoItAll.Modules.CognitiveMemory\Procedural`
- `src\CanDoItAll.Modules.CognitiveMemory\Projection`
- `src\CanDoItAll.Modules.CognitiveMemory\Recall`
- `src\CanDoItAll.Modules.CognitiveMemory\ReviewUi`
- `src\CanDoItAll.Modules.CognitiveMemory\Scoring`
- `src\CanDoItAll.Modules.CognitiveMemory\Settings`
- `src\CanDoItAll.Modules.CognitiveMemory\Signals`
- `src\CanDoItAll.Modules.CognitiveMemory\Taxonomy`
- `src\CanDoItAll.Modules.CognitiveMemory\TemporalReplay`
- `src\CanDoItAll.Modules.CognitiveMemory\Workspace`

The API route file is `src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`. It currently maps routes for status, database profiles, settings, ingestion, external files, external web links, snapshot, source ingest, consolidation, recall, review decisions, probes, self-regulation, answer gate, professor review, epistemic drive, cross-project promotions, distributed workers, and distributed jobs.

Codeanalytics snapshot:

- Snapshot id: `snap-20260518225923-20ac6533`
- Scope: cognitive memory module, Web API, and related tests.
- Result: no blocking snapshot errors; broad endpoint and test surface exists.

## Oversized Files

The current implementation has several files that are large enough to hide policy and behavioral coupling:

| File | Approximate lines | Concern |
| --- | ---: | --- |
| `Recall\CognitiveMemoryRecallServices.cs` | 3015 | Recall orchestration, vector recall, context packing, and scoring appear coupled. |
| `Advanced\CognitiveMemoryAdvancedServices.cs` | 2343 | Probe, self-regulation, professor review, epistemic drive, cross-project, and distributed work are grouped together. |
| `Pages\CognitiveMemoryPage.razor.cs` | 1850 | UI orchestration is too large for predictable Blazor state. |
| `Pages\CognitiveMemoryPage.razor` | 1450 | Page markup should be split into focused component wrappers. |
| `ReviewUi\CognitiveMemoryReviewUiService.cs` | 1119 | Review UI query/build logic and policy may be mixed. |
| `Consolidation\CognitiveMemoryConsolidationServices.cs` | 1085 | Consolidation behavior, candidate creation, and execution mechanics need clearer seams. |
| `Procedural\CognitiveMemoryProcedureSkillService.cs` | 961 | Procedural memory deserves smaller extraction/application units. |
| `TemporalReplay\CognitiveMemoryTemporalReplayServices.cs` | 950 | Replay services should be split by query, replay planning, and persistence. |
| `Settings\CognitiveMemorySettingsServices.cs` | 926 | Settings validation, defaults, and persistence are candidates for separation. |

## Behavioral Gaps To Verify

The code has many endpoints and tests, but the LB4U task stresses behavior that current static tests may not prove:

- Consolidation currently appears to lean on deterministic classification and generic payload summaries in important paths.
- Epistemic drive appears centered on answer-gate gaps and calibration aggregates; it may not deeply scan source/canonical memory for reusable business-plan or planning knowledge.
- Probe sessions appear to summarize recall context rather than generate a richer answer using a selected model and token policy.
- Cognitive memory settings include provider/agent access controls, but no clearly isolated cognitive-memory model profile that pins model id, output token budget, and truncation policy for OpenAI versus Ollama.
- External source ingestion must prove it can handle docx, PDF, PPTX, XLSX, and asset-node references without leaking sensitive files.
- The test suite proves parts exist; it does not yet prove that staged realistic project data produces useful chunks, canonical memories, and cross-project knowledge.

## Initial Conclusion

The follow-up should not start with a rewrite. It should first create a repeatable LB4U memory validation harness, then improve the narrow paths that fail observable behavior: staged extraction, chunking, model-assisted consolidation, review/probe loops, model profile settings, and maintainability splits around the largest services.
