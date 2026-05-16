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
| Score geometry driver | provide reusable typed score spaces, vectors, shapes, scalar projections, and evaluation traces instead of local weighted-sum formulas. |
| Knowledge coverage maps | represent coverage/confidence/staleness/risk by topic region. |
| Epistemic Drive engine | detect important knowledge gaps and evaluate multi-dimensional epistemic tension. |
| Learning proposal service | create human-reviewable learning proposals with evidence, sources, risks, and acceptance criteria. |
| Learning task planner | convert approved proposals into scoped learning tasks. |
| Knowledge probing integration | generate probing questions and consume probing outcomes as gap evidence. |
| Night Reflection UI | show top knowledge improvement opportunities and approval actions. |
| Evidence anchor ledger | fine-grained source grounding for claims, procedures, reviews, and regressions. |
| Claim/evidence/belief ledger | atomize truth below memory items and preserve support/attack/scope/validity state. |
| Memory mutation authority | command-based authoritative writes with idempotency, audit, review, and projection invalidation. |
| Entity/context binding | prevent semantically similar but operationally incompatible memories from merging or substituting. |
| Cognitive workspace service | active scoped working memory with focus slots, goals, open questions, and inhibition. |
| Attention router | choose recall, answer, clarification, source audit, probe, review, replay, learning proposal, or abstention. |
| Prediction error engine | record expected-vs-observed mismatches from probes, workflows, QA, procedures, and high-risk answers. |
| Salience signal ledger | preserve novelty, surprise, risk, usefulness, rework, user interest, source weakness, and calibration risk as dimensions. |
| Temporal episode/replay scheduler | preserve ordered experience and prioritize replay/rehearsal without promoting truth directly. |
| Procedural skill memory | model procedures as skill graphs with maturity, validation, failure modes, and automation policy. |
| Simulation sandbox | keep planning and analogies speculative until source-backed and reviewed. |
| Metamemory answer gate | decide whether to answer, warn, clarify, source-audit, probe, review, request learning, or abstain. |
| Cognitive self-model | store scoped competence, limits, operating principles, weak domains, known failure patterns, and self-regulation policy. |
| Self-regulation orchestrator | evaluate workspace, evidence, calibration, risk, and self-model state into assessment, required operations, and answer posture. |
| Humility trigger engine | detect when confidence must be reduced and when clarification, audit, probe, review, professor review, or abstention is required. |
| Calibration health service | aggregate confidence-vs-outcome evidence by domain, task, model profile, risk, and feature pattern. |
| Professor review service | use larger model/expert review as governed challenge/audit input, not as source truth. |

## Closed Projection-Boundary Changes to Existing RAG Driver

Implemented by `codex/bundles/cognitive-memory-projection-boundary-hardening`:

- typed filter support,
- payload index request/result contracts,
- delete by generic metadata filter,
- capability discovery for filters, payload indexes, delete-by-filter, and named vectors,
- Qdrant mapper/driver translation for filtered search and cleanup.

Remaining Cognitive Memory adapter responsibilities:

- define projection payload field names in the Cognitive Memory module,
- create payload indexes for frequently filtered projection fields,
- store collection/schema/profile version metadata in Cognitive Memory projection records,
- use existing batch upsert with deterministic projection ids and delete-by-filter for stale cleanup.

Future changes:

- named vectors,
- hybrid vector + sparse/lexical search,
- multi-vector per memory item.

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
- add Epistemic Drive scan executors,
- add learning proposal/task executors,
- add approval-gated source study workflows,
- add learning QA handoff.

## Required UI Additions

- memory dashboard,
- project memory list,
- memory item detail,
- recall trace viewer,
- consolidation run viewer,
- human review queue,
- procedure library,
- Night Reflection / Cognitive Briefing,
- knowledge coverage map,
- learning proposal detail,
- learning outcome review.

## New Epistemic Drive Constraints

- A display priority score may exist only for UI sorting.
- Core decisions must preserve vector dimensions, evidence refs, Pareto/category metadata, ROI estimate, and explanation.
- Epistemic Drive must use the generic `EpistemicNeed` score space so dimensions, shapes, normalization, and scalar projection are comparable to other cognitive score evaluations.
- Prediction errors, salience signals, replay outcomes, answer-gate decisions, and contested claims may become evidence contributors but never direct truth.
- Learning tasks must respect local-first/offline mode and source approval policy.
- Learning-derived memory remains draft until source refs and validation requirements are satisfied.
- Cross-project learning opportunities must not leak project-private source content.

## New Score Geometry Constraints

- Recall, attention, belief, salience, replay, probing, answer gating, Epistemic Drive, mindmap similarity, activation, procedure maturity, and cross-project promotion must declare score spaces.
- Self-regulation assessment, self-model competence, calibration health, professor-review routing, and answer posture must declare score spaces.
- Qdrant and SemanticCompletion similarity results are dimensions, not final ranks.
- A behavior-affecting scalar score, priority, confidence, or weight is invalid unless it is a derived `ScoreScalarProjection` linked to a `ScoreEvaluationTrace`.
- Score dimensions used for filtering, review, replay, calibration, or dashboards must be relational/indexable, not hidden only in JSON.

## New Self-Regulation Constraints

- Self-Regulation is calibrated agency under epistemic uncertainty, not consciousness or prompt persona.
- Self-model, professor review, salience, prediction error, probing feedback, and generated summaries cannot become canonical truth directly.
- Answer gate consumes self-regulation assessment/posture and can become stricter, but not looser without a new trace.
- Professor review output becomes governed evidence, probe/source-audit/regression/review/learning input, or mutation candidate only.
- Calibration health changes must be versioned and reviewable where behavior-affecting thresholds or score shapes change.
