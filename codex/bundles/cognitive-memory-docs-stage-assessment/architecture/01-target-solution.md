# Target Solution

## End State

- `docs/cognitive-memory` is the canonical documentation entry point for Cognitive Memory.
- The docs communicate validation-grade alpha accurately, with direct statements about what is done, what is only alpha, and what blocks beta.
- Mermaid diagrams explain current implementation shape without inventing future architecture.
- Existing docs link to the new section so maintainers do not need to discover scattered notes.

## Boundaries

- The docs must keep durable EF-backed memory separate from optional Qdrant/RAG projection.
- Source ingestion must be documented as read-only snapshot ingestion; canonical memory mutation must remain governed through consolidation/review/mutation command paths.
- MAF context contribution must be documented as consumer context, not a hidden canonical writer.
- Automation scheduling must be described as settings-only until a real worker or scheduler path exists.

## Allowed Side Effects

- Add and update markdown files.
- Update bundle artifacts.
- Do not change C# source, tests, migrations, Razor components, or project files.

## Documentation Shape

- `docs/cognitive-memory/README.md`
- `docs/cognitive-memory/current-state/stage-assessment.md`
- `docs/cognitive-memory/current-state/implementation-map.md`
- `docs/cognitive-memory/architecture/system-overview.md`
- `docs/cognitive-memory/architecture/domain-model.md`
- `docs/cognitive-memory/architecture/runtime-flows.md`
- `docs/cognitive-memory/architecture/integration-boundaries.md`
- `docs/cognitive-memory/operations/api.md`
- `docs/cognitive-memory/operations/validation-and-testing.md`
- `docs/cognitive-memory/roadmap/roadmap.md`
