# 21 Schema Entity Context Binding

## Purpose

Add a binding layer that resolves entities, schemas, aliases, context frames, and operational boundaries before semantic merging, recall ranking, or claim promotion.

This is necessary because semantic similarity is not identity. Many CanDoItAll concepts are intentionally similar across different scopes:

- production Docker deployment,
- test Docker simulation,
- local Docker Compose development,
- CI Docker job,
- plugin sandbox Docker runtime.

## Binding Layer Responsibilities

1. Entity extraction and resolution.
2. Alias mapping.
3. Context frame construction.
4. Scope boundary detection.
5. Schema/chunk classification.
6. Contextual identity checks.
7. Context-aware recall and projection payload enrichment.

## Entity Registry

An entity can be:

- project,
- module,
- plugin,
- workflow,
- process,
- agent,
- user/role,
- source system,
- environment,
- repository branch,
- technology topic,
- procedure target,
- business object,
- artifact.

Entity records should track:

- canonical name,
- aliases,
- entity type,
- project/global scope,
- source anchors,
- confidence,
- validity window,
- owner/reviewer,
- related context frames.

## Context Frame

A context frame describes where a claim/procedure/episode is valid.

Recommended frame dimensions:

| Dimension | Examples |
|---|---|
| Project | CanDoItAll, Zyphonote, local experiment. |
| Environment | production, staging, local, CI, test simulation. |
| Runtime | Docker, WSL, Windows 11, Linux server. |
| Process | architecture review, implementation, QA, deployment. |
| Role | architect, QA, HR agent, workflow executor. |
| Time | version/date validity range. |
| Source trust | local source, official docs, generated summary. |
| Risk | security, finance, destructive automation, low risk. |
| Access scope | project-private, global reusable, redacted. |

## Context Boundaries

A context boundary is a rule saying that two memory candidates are related but should not be merged or substituted.

Example:

```text
Boundary: EnvironmentBoundary
A: Docker Compose for local plugin development
B: Docker deployment procedure for production
Policy: related but not substitutable
Recall behavior: show B as side context only if query asks production; inhibit A as authoritative answer
```

## Schema Binding

The system should classify chunks by schema:

- fact/claim,
- decision,
- procedure,
- problem/failure,
- requirement,
- design rationale,
- configuration,
- code pattern,
- test evidence,
- policy,
- open question,
- hypothesis.

Schema binding affects:

- evidence requirements,
- validation policy,
- recall format,
- projection type,
- probing question type,
- learning proposal output.

## Integration Points

### Ingestion

Source canonicalization should call the binding layer before creating canonical memory items or claims.

### Mindmap Processing

Mindmap spatial/graph features should feed context frames. Parent branches, nearby clusters, link types, and object metadata are context signals.

### Recall

Recall should filter and rank by context frame compatibility before final context pack rendering.

### Probing

Context-separation drill should explicitly target boundary rules and verify that the system does not substitute wrong context.

### Projection

Qdrant payload should include entity ids, context frame ids, schema type, and context boundary flags so vector search can be filtered and interpreted.

## Required Acceptance Criteria

- Semantically similar records in different environments can be related but not merged.
- Recall trace explains when candidates were inhibited by context boundary.
- Entity alias resolution is visible and auditable.
- Source anchors support entity decisions.
- Cross-project memory cannot merge project-private entities without policy approval.
