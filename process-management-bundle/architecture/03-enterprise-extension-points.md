# Enterprise Extension Points

The additional architecture notes are treated here as mandatory design constraints, not optional future wishes.

## Concern Coverage Matrix

| Concern | Must model now | Can defer enforcement later |
| --- | --- | --- |
| Explainability and decision transparency | Decision records, assignment reasons, policy evaluation records, escalation reasons, process selection records | richer scoring, executive narratives, and automated explanation generation |
| Decision intelligence | outcome links, role-executor fit metadata, validation-strength metadata, cost-quality metadata | full evaluator services and optimization recommendations |
| Artifact trust model | artifact snapshot, trust state, validation record, approval record, lineage, sensitivity, retention, usage policy | full policy automation, storage migration, and training-pipeline enforcement |
| Capability gap detection | role requirement, executor capability snapshot, failed assignment reason, bottleneck and rework markers | deeper analytics, staffing recommendations, and auto-generated improvement proposals |
| Simulation and safe preview | versioned definitions, policy boundaries, replay input packages, scenario metadata hooks | actual dry-run engine and predictive estimates |
| Autonomy governance | autonomy level, permission scope, approval requirement, operating mode, refusal reason | runtime policy adapters and dynamic permission narrowing |
| Forensic reconstruction | runtime event journal, evidence package IDs, tool invocation references, environment snapshot references | full replay UI and export packaging |
| Process lifecycle | draft, under review, approved, pilot, active, deprecated, archived, superseded | richer diff tooling and impact forecasting |
| Anti-fragility and learning | learning signals, improvement candidate records, repeat-failure markers | auto-ranked recommendations and adaptive tuning |
| Execution economics | cost-record and attribution seams | full billing or finance dashboards |
| Operating modes | sandbox, development, guided, semi-autonomous, production, high-assurance, forensic review | full environment policy matrices |
| Relationship layer between executors | handoff quality, collaboration-risk signal, compatibility markers | optimizer and ranking engines |
| Safe refusal / non-action | refusal reason, missing prerequisite record, approval block record, trust-threshold failure | automated escalation orchestration |
| Executive / management UX | management-readable process health surfaces and typed aggregates | polished executive dashboards |
| Constitution / fundamental rules | non-overridable rule model, governance priority, irreversible action policy | centralized rule authoring UI |

## Data That Must Already Be Collected In Early Phases

- Selected process definition and version for every run
- Selected role requirement and assignment rationale
- Eligible pool or fallback summary when assignment resolution occurs
- Decision-right and approval outcomes
- Artifact trust and evidence references
- Wait reason, refusal reason, exception reason, and escalation reason
- Operating mode and autonomy envelope in effect
- Correlation identifiers for any external runtime activity

## Why This Matters

- If these structures are omitted now, later enterprise-grade governance will require reworking the core runtime and persistence model.
- If these structures exist now, the first delivery can still stay small while keeping the path open for simulation, audit, trust, and optimization later.
