# Existing vs New Capabilities

## Reuse Without Major Change

| Capability | Existing source | Reuse mode |
|---|---|---|
| Module assembly registration | main CanDoItAll composition | add module assembly. |
| EF model discovery | infrastructure persistence | add configuration classes. |
| File/IPFS/FTP storage | infrastructure storage drivers | store snapshots and reports. |
| Keyword search | infrastructure search index | lexical recall channel. |
| Workbench project objects | Workbench module | mindmap/source graph input. |
| Workbench links | Workbench module | graph feature input. |
| Process runtime records | Processes module | episodic/procedural source input. |
| Workflow runtime | AgentFramework Core/MAF | recall/consolidation workflows. |
| Workflow executors | AgentFramework Core | expose memory tools as nodes. |
| Plugin executor pattern | Plugins module | source connectors and procedures. |
| OAuth/plugin host tools | Plugins module | source ingestion from external services. |
| RAG driver | standalone RAG repo | vector projection adapter. |
| Qdrant driver | standalone RAG repo | projection backend. |
| ONNX embeddings/ranker | SemanticCompletion repo | semantic provider adapter. |
| Automation/Quartz | Automation/Scheduler modules | consolidation scheduling. |

## New Required Capabilities

| Capability | Why needed |
|---|---|
| Source manifest | stable identity, hashing, provenance. |
| Canonical memory records | normalize source meaning. |
| Memory graph relations | explicit non-vector associations. |
| Memory activation model | human-like recall prioritization. |
| Recall orchestrator | staged retrieval and attention. |
| Consolidation engine | idle/night memory refinement. |
| Mindmap feature extractor | spatial/graph/semantic signals. |
| Projection manager | rebuildable Qdrant indexing. |
| Human review queue | trust and ambiguity management. |
| Recall trace model | explainable agent memory. |
| Distributed job protocol | LAN idle compute. |
| MAF memory context provider | agent context injection. |
| Workflow memory executors | use memory inside workflows. |
| Procedure extraction | convert successful runs into reusable knowledge. |
| Contradiction/supersession logic | avoid stale or conflicting truth. |

## Required Changes to Existing RAG Driver

Minimum V1 changes:

- add filter support,
- add delete by metadata/source id support,
- add projection metadata fields,
- add batch upsert lifecycle,
- add collection schema/version metadata.

Future changes:

- named vectors,
- hybrid vector + sparse/lexical search,
- multi-vector per memory item,
- payload index setup helper.

## Required Changes to Workbench Model

V1:

- read Z from metadata if present.

V1.1:

- add explicit `PositionZ` if 3D mindmaps become core.

## Required Changes to MAF Integration

- add cognitive context provider,
- add memory tools,
- add workflow executors,
- add recall trace persistence,
- add post-run reflection hook.

## Required UI Additions

- memory dashboard,
- project memory list,
- memory item detail,
- recall trace viewer,
- consolidation run viewer,
- human review queue,
- procedure library.
