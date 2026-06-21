# .NET architecture design and review subprocess

**Key:** `dotnet-architecture-design-review`
**Criticality:** High
**Autonomy level:** Guarded

Splits .NET architecture design from independent architecture review before implementation starts.

## Value
Improves maintainability and smaller-model reliability by forcing explicit app-type classification, design drafting, review challenge, and implementation-ready handoff.

## Permission model
Every step declares explicit operations and target scope so role permissions remain bounded and product mutation cannot leak into planning, review, validation, screenshot, or writeback work.

## Steps
### 1. Classify .NET application type and project boundary (`classify-dotnet-application`)
- Step kind: Start
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: None
- Outputs: Typed .NET application classification with product root, test root, runtime surfaces, and UI/no-UI applicability.
- Evidence: Project context, app type, route/runtime inventory, contradictions, and assumptions.

### 2. Draft .NET architecture design (`draft-architecture-design`)
- Step kind: Work
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: classify-dotnet-application
- Outputs: Reviewable .NET architecture design draft with boundaries, service model, data model, runtime assumptions, and test strategy.
- Evidence: Design options, selected approach, models, services, boundaries, test seams, and rejected alternatives.

### 3. Review .NET architecture design (`review-architecture-design`)
- Step kind: Review
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: classify-dotnet-application, draft-architecture-design
- Outputs: Reviewed architecture decision with required implementation constraints, approval rationale, or hard block reason.
- Evidence: Checklist answers, design risks, testability assessment, implementation constraints, and go/no-go architecture recommendation.

### 4. Hand off reviewed .NET architecture (`architecture-handoff`)
- Step kind: End
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: classify-dotnet-application, review-architecture-design
- Outputs: Parent-ready architecture handoff for .NET implementation slice routing.
- Evidence: Accepted design, unresolved risks, mandatory implementation constraints, implementation start criteria, runtime command expectations, and UI screenshot applicability.
