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

## Follow-up Functional Requirements

- RQ-021: Provide a clean PostgreSQL workflow-routing test datasource and a Visual Studio launch profile that uses it without affecting unrelated databases.
- RQ-022: Add first-class decision block entries for IF/ELSE, SWITCH/default, and fan-out to both the workflow toolbox and the canvas right-click second-layer menu.
- RQ-023: Render decision blocks as visually distinct diamond nodes with intuitive branch connectors and readable branch labels.
- RQ-024: Improve first-create floating setup dialogs for decision nodes and other workflow blocks with meaningful typed settings.
- RQ-025: Introduce a renderer-keyed setup-dialog seam so future plugins/executor blocks can supply specialized setup content instead of relying on one generic dialog.
- RQ-026: Seed production-like workflow examples for document summarization, email processing variants, XLSX read/write scenarios, internet fetch into project structure, and at least 5-10 additional useful workflows.
- RQ-027: Tune LLM component prompts/settings and workflow runtime settings so examples are production-oriented rather than demo-only.
- RQ-028: Execute or simulate at least 20 real-world workflow scenarios, compare expected decision outcomes with `gpt-5-mini` and Ollama `gptoss20b64k`, record observed issues, and repair implementation or bundle notes.
