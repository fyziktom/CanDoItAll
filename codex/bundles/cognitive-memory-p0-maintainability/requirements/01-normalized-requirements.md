# Normalized Requirements

| ID | Requirement | Observable success criteria | Owning subbundle |
| --- | --- | --- | --- |
| CM-P0-001 | Split oversized Cognitive Memory services/API/page surfaces by use case. | Large files are reduced or decomposed into focused partial/service/DTO files; build and targeted tests pass. | `01-refactor-oversized-surfaces` |
| CM-P0-002 | Add explicit projection rebuild execution. | A service/API path consumes rebuild-required projections, calls the projection lifecycle, and reports projected/failed/skipped outcomes. | `02-projection-rebuild-and-scheduled-automation` |
| CM-P0-003 | Implement scheduled automation execution semantics. | A service respects `CognitiveMemoryAutomationScheduleMode`, triggers source ingestion/consolidation explicitly, and exposes run summaries. | `02-projection-rebuild-and-scheduled-automation` |
| CM-P0-004 | Separate agent-facing Cognitive Memory context from diagnostic recall payloads. | MAF contributor uses a dedicated agent context package/builder instead of exposing diagnostic recall records directly. | `03-agent-context-policy-and-dtos` |
| CM-P0-005 | Make process-critical memory contribution fail/skip policy explicit. | Tests prove process-critical mode fails on unavailable required memory while normal interactive modes can skip with metadata. | `03-agent-context-policy-and-dtos` |
| CM-P0-006 | Validate the P0 implementation. | Targeted tests/build/diff checks and bundle validators pass. | `04-docs-validation-and-closure` |
| CM-P0-007 | Update docs and roadmap to the real post-P0 state. | Cognitive Memory docs and roadmap reflect what changed and what remains. | `04-docs-validation-and-closure` |
