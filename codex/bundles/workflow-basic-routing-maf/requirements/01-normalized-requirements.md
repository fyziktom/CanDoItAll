# Normalized Requirements

## Functional Requirements

- RQ-001: Add a typed routing contract to workflow edges for direct, predicate, switch case, switch default, and fan-out selector routes.
- RQ-002: Preserve existing `ConditionExpression` and old definitions without route metadata.
- RQ-003: Compile predicate routes through MAF `AddEdge<T>` with a predicate delegate.
- RQ-004: Compile switch case/default routes through MAF switch routing.
- RQ-005: Compile multi-selection routes through MAF fan-out target selector routing.
- RQ-006: Evaluate built-in predicates deterministically against `WorkflowNodeInput.PayloadJson`.
- RQ-007: Add workflow canvas route-authoring controls for direct, IF predicate, switch, default, and fan-out routes.
- RQ-008: Display route summaries/labels in the edge inspector and preferably on the canvas link or edge list.
- RQ-009: Persist and expose route metadata through catalog/API round-trip.
- RQ-010: Add realistic route scenarios for IF/ELSE, SWITCH/default, and fan-out.

## Safety And Compatibility Requirements

- RQ-011: Do not execute arbitrary C#, JavaScript, reflection expressions, or user scripts.
- RQ-012: Reject unsupported `artl-v1` routes until an ARTL compiler is implemented.
- RQ-013: Do not silently downgrade invalid route definitions to direct edges.
- RQ-014: Keep route contracts serializable and versionable.
- RQ-015: Keep compiler and UI seams ready for future ARTL replacement.

## Proof Requirements

- RQ-016: Unit tests for model serialization/defaulting and validation.
- RQ-017: Runtime tests proving branch execution behavior.
- RQ-018: Component tests for route authoring UI.
- RQ-019: Integration tests for persistence/API round-trip.
- RQ-020: Browser proof for route authoring, save/load, validation, and preview-run.
