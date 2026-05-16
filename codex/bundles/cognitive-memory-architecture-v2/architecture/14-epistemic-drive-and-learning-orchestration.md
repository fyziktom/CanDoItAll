# Epistemic Drive And Learning Orchestration

## Purpose

Epistemic Drive is the metacognitive layer that lets Cognitive Memory notice important weakness in its own knowledge. It is not random curiosity. It is policy-controlled, evidence-driven, value-driven analysis of where the system's current knowledge is weak relative to current and expected work.

The layer turns recall traces, consolidation evidence, workflow failures, user corrections, stale records, contradictions, probing results, and project direction signals into human-reviewable learning proposals.

## Problem Statement

A passive RAG database can retrieve known chunks, but it does not know when its knowledge is incomplete, stale, risky, or strategically misaligned with active work. Cognitive Memory needs a disciplined mechanism that can say:

```text
This topic is frequently used, currently weak in specific subareas, high impact if wrong, and supported by trustworthy sources. A scoped learning task would materially improve future recall and procedure quality.
```

The goal is to detect epistemic tension: the pressure created when demand, risk, uncertainty, failure recurrence, staleness, contradiction pressure, and project relevance exceed current coverage and confidence.

## Non-Goals

- Do not create autonomous internet-learning behavior.
- Do not let generated summaries become source truth.
- Do not replace human review for high-impact memory changes.
- Do not collapse the model into a single weighted score.
- Do not make Qdrant, embeddings, or clusters authoritative.
- Do not study low-value topics only because they are uncertain.

## Relationship To Consolidation

Epistemic Drive runs during idle or nightly consolidation after activation, staleness, contradiction, and supersession analysis.

Recommended stage:

```text
update activation/staleness/contradictions
  -> extract weak-topic evidence
  -> update knowledge coverage maps
  -> detect knowledge gap regions
  -> intersect gaps with active project directions
  -> evaluate epistemic tension
  -> select Pareto/ROI learning candidates
  -> create human-reviewable learning proposals
  -> generate probing question candidates
  -> update projections only after durable records exist
```

Consolidation may create `KnowledgeGapRecord`, `KnowledgeCoverageMapRecord`, `EpistemicTensionRecord`, and draft `KnowledgeNeedProposal` records. It must not execute external study or promote learning-derived memory without policy approval.

## Relationship To Recall Traces

Recall is an evidence source, not a direct memory mutator. It contributes signals such as:

- low-confidence candidate selection,
- repeated missing source references,
- high user correction rate,
- failed answer validation,
- repeated fallback to broad or generic sources,
- uncertain contradiction resolution,
- budget exclusions that repeatedly hide needed source detail,
- repeated retrieval of stale records for active tasks.

The recall orchestrator records these signals in traces. Epistemic Drive consumes the traces later and decides whether the pattern is meaningful.

## Relationship To Knowledge Probing

Knowledge Probing is bidirectional with Epistemic Drive:

- probing sessions reveal gaps, uncertain concepts, and weak procedures,
- Epistemic Drive generates probing question sets for candidate gap regions,
- failed probing answers increase gap density or confidence weakness,
- successful probing can increase confidence or coverage,
- probing can be requested before learning to confirm that study is necessary,
- probing can be used after learning to validate improvement.

Probing failures are evidence, not automatic truth. They should create gap evidence and review context rather than overwrite validated memory.

## Relationship To MAF Workflows

MAF orchestrates learning work. It does not own durable memory.

Recommended workflow shape:

```text
Epistemic Drive scan
  -> Human Approval Gate
  -> Learning Planner Agent
  -> approved Source Study Agent work
  -> Procedure/Runbook Miner Agent
  -> Learning QA Agent
  -> Human review for high-impact outputs
  -> durable memory update by Cognitive Memory services
  -> projection refresh
```

MAF agents can execute approved tasks, produce reports, and submit draft canonical records. Only Cognitive Memory authority services can persist canonical memory, relation state, coverage maps, and projections.

## Relationship To Human Review

Every learning proposal is reviewable before external study or major memory changes.

Supported decisions:

- approve,
- reject,
- snooze,
- narrow scope,
- expand scope,
- add sources,
- request probing first,
- turn proposal into a Codex bundle,
- assign the task to a human or agent.

High-risk procedure changes, security guidance, deployment procedures, secret handling rules, compliance guidance, and destructive automation instructions require human validation before becoming active memory.

## Relationship To Qdrant Projections

Qdrant remains a rebuildable projection only.

Epistemic Drive may use projection-derived clusters and similarity as weak evidence for topic regions, but no gap, proposal, or learning output is authoritative because it exists in Qdrant. Durable records must store source refs, evidence refs, algorithm versions, and policy decisions in relational/storage state. Projections are refreshed only after durable memory state changes.

## Evidence Sources

Gap detection should consume typed evidence refs:

- recall trace ids,
- workflow run ids,
- process run ids,
- source item ids,
- canonical memory item ids,
- contradiction ids,
- probing session ids,
- user correction ids,
- project direction ids,
- source candidate ids,
- human review item ids.

Evidence refs should include the observed signal, timestamp, weight or confidence, and a short explanation. They should not store secret values.

## Multi-Dimensional Model

The core model is a vector and evidence set, not a scalar.

`KnowledgeNeedVector` should preserve these dimensions:

| Dimension | Meaning |
|---|---|
| `UsageFrequency` | How often the topic appears in recall, work, workflows, or project artifacts. |
| `ConfidenceWeakness` | How weak current validated knowledge is. |
| `RiskImpact` | Harm if the system gives wrong guidance. |
| `Staleness` | Likelihood that current knowledge is outdated. |
| `FailureRecurrence` | Repeated failures, rework, or recovery episodes. |
| `StrategicAlignment` | Relevance to active or planned project directions. |
| `QuestionDensity` | Open questions per topic/subtopic. |
| `BusinessValue` | Expected operational value of improved knowledge. |
| `EstimatedLearningEffort` | Expected cost to improve coverage. |
| `SourceAvailability` | Whether approved sources are available. |
| `SourceQuality` | Trustworthiness of sources. |
| `ContradictionPressure` | Unresolved conflict density. |
| `UserInterestSignal` | User corrections, asks, approvals, or repeated focus. |
| `Volatility` | How likely the topic changes over time. |
| `ExpectedReuse` | Expected future use across tasks/projects. |

A display priority score may be computed for UI sorting, but it is secondary. The system must keep vector components, evidence refs, category, Pareto rank, ROI estimate, and explanation text.

## Geometric And Project-Direction Model

A topic is a region, not a single point. A `KnowledgeRegion` can have child regions, source coverage, confidence, risk, and project-direction intersections.

Example Docker region:

- CLI basics,
- Dockerfile,
- Compose,
- volumes,
- networking,
- secrets/configs,
- Swarm,
- registry,
- build cache,
- troubleshooting,
- non-happy paths.

Each subregion can have different coverage, confidence, usage, and risk. Weak regions become important when they intersect with active directions such as plugin runtime isolation, local development setup, deployment automation, workflow executor sandboxing, or DevOps troubleshooting.

`ProjectDirectionVector` should come from project graph/mindmap source truth, active process/workflow needs, explicit user priorities, and roadmap artifacts. Region intersection should be explainable: which weak subareas overlap which active work and why.

## Candidate Selection Methods

Use multiple decision methods:

- Pareto frontier selection to find candidates that are not dominated across risk, usage, uncertainty, source availability, and effort.
- Region intersection to raise candidates whose weak subregions overlap active project directions.
- Learning ROI estimates to plan scope and expected outputs.
- Category classification for operator triage.
- Scalar display score only for secondary UI sorting.

Suggested categories:

| Category | Meaning | Default action |
|---|---|---|
| `HighTensionHighRoi` | Important, weak, and feasible to improve. | Study soon after approval. |
| `HighTensionLowRoi` | Important but expensive or source-poor. | Ask user/expert or narrow scope. |
| `LowTensionHighRoi` | Cheap improvement but not urgent. | Opportunistic learning. |
| `LowTensionLowRoi` | Not worth attention now. | Ignore, archive, or revisit later. |
| `HighUncertaintyLowUsage` | Known unknown without current demand. | Track, do not study yet. |
| `HighUsageHighConfidence` | Current coverage is sufficient. | Maintain only. |

## Learning Proposal Lifecycle

```text
Evidence observed
  -> feature extraction
  -> knowledge coverage map refresh
  -> gap region detection
  -> epistemic tension evaluation
  -> candidate classification
  -> draft learning proposal
  -> human decision
  -> scoped learning task
  -> approved source study
  -> draft canonical records/procedures/questions
  -> QA and human review where required
  -> durable memory update
  -> projection refresh
  -> coverage map update
```

Each proposal should include:

- topic and subtopic coverage map,
- evidence summary,
- why this matters now,
- uncertainty and gap explanation,
- related project directions,
- suggested sources,
- source trust level,
- estimated effort,
- expected outputs,
- proposed depth,
- risks,
- required approvals,
- suggested probing questions,
- suggested acceptance criteria.

## Learning Task Outputs

Approved learning workflows can produce:

- source-grounded canonical knowledge records,
- procedural records and runbooks,
- non-happy-path troubleshooting notes,
- probing question sets,
- source-grounded examples,
- coverage map updates,
- learning outcome report,
- human review items for high-impact changes.

Learning output is draft until validated. Every canonical item must carry source refs. Generated summaries are never source truth.

## Safety Rules

1. Learning proposals require human approval when external study, high-impact updates, or policy-sensitive areas are involved.
2. Source trust must be classified before use.
3. External source reading must respect project policy and access controls.
4. Do not ingest secrets or project-private content into cross-project learning without approval.
5. Do not silently replace human-validated records.
6. Contradictions must remain visible until resolved or accepted as ambiguity.
7. High-risk procedures require human validation before activation.
8. Distributed workers may compute clusters, embeddings, feature summaries, or candidate evidence, but cannot mutate authoritative memory.
9. Learning tasks must be idempotent and resumable with input hashes, source versions, and algorithm versions.
10. Projection refresh happens after durable memory writes, not before.

## Auditability

Audit records should answer:

- why this topic,
- why now,
- which evidence triggered it,
- which subregions are weak,
- which project directions it intersects,
- which sources were proposed and approved,
- who approved scope,
- what the learning task read,
- what outputs were created,
- which outputs were rejected or promoted,
- which projections were refreshed.

## Local-First Behavior

Epistemic Drive must work without internet access. In local-first mode it can use:

- project docs,
- repository files,
- uploaded files,
- approved local source snapshots,
- internal knowledge bases,
- previously stored source items,
- workflow/process run evidence.

If no approved sources exist, the proposal should request sources or probing instead of trying to study externally.

## Cross-Project Behavior

Cross-project memory may aggregate repeated gaps across projects into reusable learning opportunities. It must preserve project boundaries:

- project-private source content cannot leak into global proposals,
- evidence summaries must obey access policy,
- global learning outcomes need source refs that are approved for global reuse,
- project-specific confidence and coverage remain distinct from global confidence.

## Distributed Idle Compute

Idle workers may calculate embeddings, clusters, region candidates, coverage projections, or candidate gap evidence. The main memory authority must validate hashes, source scope, algorithm versions, and policy before accepting output. Workers never write canonical records, review decisions, learning proposals, or Qdrant points directly.

## Docker Scenario

Topic: Docker operational knowledge.

Current coverage:

- CLI basics: high confidence.
- Compose lifecycle: medium confidence.
- Volumes: weak to medium-low.
- Networking: weak and high-risk.
- Secrets/configs: weak.
- Swarm: missing or weak.
- Troubleshooting and non-happy paths: fragmented.

Reason for proposal:

- Docker appears often in deployment, plugin isolation, workflow executor sandboxing, and local development.
- Some workflow failures or uncertainty traces relate to Compose, networking, volumes, or platform-specific behavior.
- Official Docker documentation is available and high quality.
- Weak Docker networking or secret guidance can create production and security risk.

Example proposal text:

```text
I reviewed Docker-related knowledge during nightly consolidation. Docker is frequently used in plugin isolation, deployment, local development, and workflow executor sandboxing. Current coverage is good for basic commands, but weak for networking, volumes, secrets, Docker Compose non-happy paths, and Docker Swarm. I recommend a focused learning task based on official Docker documentation. Estimated focused pass: about one hour. Should I create and run this learning task?
```

Suggested probing questions:

- What changes when `docker run` uses `--network host` instead of bridge?
- How do bind mounts behave differently on Windows, WSL, Linux, and Docker Desktop?
- What are safe restart policies for development versus production?
- How should secrets be handled in Docker Compose for local CanDoItAll plugins?
- What are common Docker Compose startup failure modes?
- When is Docker Swarm still appropriate compared with plain Compose or Kubernetes?

Suggested outputs:

- canonical knowledge records,
- procedures and runbooks,
- non-happy-path troubleshooting notes,
- probing questions,
- source-grounded examples,
- coverage map update.

## MVP

MVP should implement:

- durable coverage map and gap records,
- vector component preservation,
- evidence refs from recall traces, workflow failures, contradictions, stale records, user corrections, and probing sessions,
- nightly `EpistemicDriveScan` mode,
- learning proposal review UI,
- approval, reject, snooze, narrow scope, and request probing actions,
- approved-source-only learning task planning,
- audit records and validation checks.

## Future Roadmap

Future versions can add:

- richer geometric region visualization,
- cross-project gap aggregation with policy-controlled source sharing,
- active expert routing for high-tension low-ROI areas,
- adaptive probing difficulty,
- learning ROI calibration from actual task outcomes,
- named-vector and hybrid sparse/vector projection support,
- team-level knowledge stewardship and assignment workflows.

## Interactive Probing As Epistemic Drive Actuator

Epistemic Drive should not only create learning proposals. It should also create probe requests when more evidence is needed before study.

Recommended flow:

```text
weak region or high-tension uncertainty
  -> generate probing question set
  -> user/system runs probing session
  -> probe outcomes classified
  -> gap evidence and calibration records created
  -> Epistemic Drive re-evaluates whether to learn, narrow scope, ask for sources, or mark coverage sufficient
```

This allows the system to avoid unnecessary learning work. Sometimes a topic appears weak because the system has not been asked the right question, or because correct source memory exists but recall scoring is miscalibrated. Probing helps distinguish missing knowledge from recall failure.

## Probe-Derived Evidence Weighting

Probe evidence should affect vector dimensions without replacing the vector model:

| Probe outcome | Likely vector effects |
|---|---|
| Missing knowledge | Raises confidence weakness and question density. |
| Wrong scope | Raises contradiction/context-separation pressure and calibration risk. |
| Overconfident incorrect answer | Raises failure recurrence and risk impact. |
| Confirmed source-backed answer | May improve coverage/confidence evidence. |
| Needs source review | Raises source availability/source quality concern. |
| User repeatedly probes topic | Raises user interest signal and expected reuse. |

Probe evidence must remain explainable and auditable.

## Neuro-Cognitive Evidence Inputs

Epistemic Drive must consume neuro-cognitive records as evidence contributors without weakening its vector model:

- cognitive signal vectors,
- prediction errors,
- answer-gate warnings and abstentions,
- workspace open questions,
- claim belief states and contested/attacked claims,
- replay outcomes,
- procedure skill maturity and failure modes,
- source anchor weakness,
- context-boundary inhibition frequency.

`KnowledgeNeedVector` must store or reference dimension schema version, normalization profile, evidence contributors, missing-dimension policy, and calculation confidence. A display score remains optional UI data only.

Examples:

- repeated wrong-scope prediction errors raise context-separation pressure and calibration risk,
- high rework cost raises risk impact and business value,
- source weakness signals raise source quality concern,
- answer-gate abstention raises source availability or confidence weakness,
- replay failures raise failure recurrence,
- procedure skill usefulness raises expected reuse.

Epistemic Drive can request probing, source audit, replay, or learning proposal. It still cannot start external study or promote learning outputs without approval.
